using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(PlayerInput))]
public class PlayerGrabSystem : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask grabbableLayer;
    [SerializeField] private float grabDistance = 2f;
    [SerializeField] private float toolUseDistance = 3f;
    [SerializeField] private Vector3 holdOffset = new Vector3(0, 1, 1);

    private IGrabbable heldObject;
    private ITool heldTool;
    private Transform holdPoint;
    private ulong heldObjectNetId;
    private PlayerInput playerInput;

    public Transform HoldPoint => holdPoint;
    public bool IsHoldingObject => heldObject != null;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        playerInput = GetComponent<PlayerInput>();
        CreateHoldPoint();
    }

    void CreateHoldPoint()
    {
        GameObject holdPointObj = new GameObject("HoldPoint");
        holdPoint = holdPointObj.transform;
        holdPoint.SetParent(transform);
        holdPoint.localPosition = holdOffset;
    }

    void Update()
    {
        if (!IsOwner) return;

        if (heldObject == null)
        {
            if (LevelGrid.Instance != null)
            {
                LevelGrid.Instance.HideHighlight();
            }

            if (playerInput.GrabPressed)
            {
                TryGrab();
            }
        }
        else
        {
            // Check if holding a tool
            if (heldTool != null)
            {
                // Tool mode: highlight targeted grid cell
                UpdateToolPreview();

                if (playerInput.GrabPressed)
                {
                    TryUseTool();
                }

                if (playerInput.DropPressed)
                {
                    Drop();
                }
            }
            else
            {
                // Regular object mode: show placement preview
                UpdatePlacementPreview();

                if (playerInput.PlacePressed)
                {
                    TryPlace();
                }

                if (playerInput.DropPressed)
                {
                    Drop();
                }
            }
        }
    }

    public void GrabSpecificObject(ulong objectId)
    {
        if (!IsOwner) return;
        
        // Don't grab if already holding something
        if (heldObject != null)
        {
            return;
        }
        
        GrabObjectServerRpc(objectId);
    }

    public bool CanInteractWithStation()
    {
        // Don't allow station interaction if there's a grabbable object nearby
        IGrabbable nearbyGrabbable = FindNearestGrabbable();
        return nearbyGrabbable == null && heldObject == null;
    }

    Vector2Int GetTargetGridPosition()
    {
        if (LevelGrid.Instance == null) return Vector2Int.zero;
        if (holdPoint == null) return Vector2Int.zero;

        // Simply use where the hold point actually is
        return LevelGrid.Instance.WorldToGrid(holdPoint.position);
    }

    void UpdatePlacementPreview()
    {
        PlaceableObject placeableObj = heldObject as PlaceableObject;
        if (placeableObj == null || LevelGrid.Instance == null) return;

        Vector2Int gridPos = GetTargetGridPosition();
        
        // Safety check: if grid position is way out of bounds, don't try to place
        if (!LevelGrid.Instance.IsInBounds(gridPos))
        {
            placeableObj.SetPlacementPreview(false);
            LevelGrid.Instance.HideHighlight();
            return;
        }
        
        bool canPlace = LevelGrid.Instance.CanPlaceAt(gridPos);
        placeableObj.SetPlacementPreview(canPlace);
        LevelGrid.Instance.ShowHighlight(gridPos, canPlace);
    }

    void UpdateToolPreview()
    {
        if (LevelGrid.Instance == null) return;

        Vector2Int gridPos = GetTargetGridPosition();
        PlaceableObject placeableObj = LevelGrid.Instance.GetObjectAt(gridPos);
        
        if (placeableObj != null && placeableObj.IsPlaced)
        {
            // Valid target - show tool highlight
            LevelGrid.Instance.ShowToolHighlight(gridPos, true);
        }
        else
        {
            // Empty cell - hide highlight
            LevelGrid.Instance.HideHighlight();
        }
    }

    void TryGrab()
    {
        // Don't grab if already holding something
        if (heldObject != null)
        {
            return;
        }

        IGrabbable grabbable = FindNearestGrabbable();

        if (grabbable != null)
        {
            NetworkObject netObj = (grabbable as Component).GetComponent<NetworkObject>();
            if (netObj != null)
            {
                GrabObjectServerRpc(netObj.NetworkObjectId);
            }
        }
    }

    IGrabbable FindNearestGrabbable()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, grabDistance, grabbableLayer);

        if (nearbyObjects.Length == 0) return null;

        Collider closest = nearbyObjects[0];
        float closestDist = Vector3.Distance(transform.position, closest.transform.position);

        foreach (Collider col in nearbyObjects)
        {
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < closestDist)
            {
                closest = col;
                closestDist = dist;
            }
        }

        return closest.GetComponent<IGrabbable>();
    }

    [ServerRpc]
    void GrabObjectServerRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            IGrabbable grabbable = netObj.GetComponent<IGrabbable>();

            if (grabbable != null && grabbable.CanBeGrabbed())
            {
                netObj.TrySetParent(holdPoint);
                grabbable.OnGrabbed(holdPoint);
                SetHeldObjectClientRpc(objectId);
            }
        }
    }

    [ClientRpc]
    void SetHeldObjectClientRpc(ulong objectId)
    {
        if (!IsOwner) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            heldObject = netObj.GetComponent<IGrabbable>();
            heldTool = netObj.GetComponent<ITool>();
            heldObjectNetId = objectId;

            if (heldTool != null)
            {
                Debug.Log($"Equipped tool: {heldTool.GetToolName()}");
            }
        }
    }

    void TryUseTool()
    {
        if (heldTool == null || !heldTool.CanBeUsed()) return;
        if (LevelGrid.Instance == null) return;

        Vector2Int gridPos = GetTargetGridPosition();
        PlaceableObject placeableObj = LevelGrid.Instance.GetObjectAt(gridPos);
        
        if (placeableObj != null)
        {
            NetworkObject targetObj = placeableObj.GetComponent<NetworkObject>();
            if (targetObj != null)
            {
                UseToolServerRpc(heldObjectNetId, targetObj.NetworkObjectId);
            }
        }
    }

    [ServerRpc]
    void UseToolServerRpc(ulong toolId, ulong targetId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(toolId, out NetworkObject toolObj) &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObj))
        {
            ITool tool = toolObj.GetComponent<ITool>();
            if (tool != null && tool.CanBeUsed())
            {
                tool.OnUse(targetObj.gameObject);
            }
        }
    }

    void TryPlace()
    {
        if (LevelGrid.Instance == null) return;

        Vector2Int gridPos = GetTargetGridPosition();

        if (LevelGrid.Instance.CanPlaceAt(gridPos))
        {
            PlaceObjectServerRpc(heldObjectNetId, gridPos);
        }
    }

    void Drop()
    {
        DropObjectServerRpc(heldObjectNetId);
        if (LevelGrid.Instance != null)
        {
            LevelGrid.Instance.HideHighlight();
        }
    }

    [ServerRpc]
    void PlaceObjectServerRpc(ulong objectId, Vector2Int gridPos)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            IGrabbable grabbable = netObj.GetComponent<IGrabbable>();
            if (grabbable != null)
            {
                netObj.TryRemoveParent();
                grabbable.OnPlaced(gridPos);
                ClearHeldObjectClientRpc();
            }
        }
    }

    [ServerRpc]
    void DropObjectServerRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            IGrabbable grabbable = netObj.GetComponent<IGrabbable>();
            if (grabbable != null)
            {
                netObj.TryRemoveParent();
                grabbable.OnDropped();
                ClearHeldObjectClientRpc();
            }
        }
    }

    [ClientRpc]
    void ClearHeldObjectClientRpc()
    {
        if (!IsOwner) return;

        heldObject = null;
        heldTool = null;
        heldObjectNetId = 0;

        if (LevelGrid.Instance != null)
        {
            LevelGrid.Instance.HideHighlight();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabDistance);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * toolUseDistance);
    }
}