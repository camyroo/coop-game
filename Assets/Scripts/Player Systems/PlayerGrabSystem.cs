using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(PlayerInput))]
public class PlayerGrabSystem : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask grabbableLayer;
    [SerializeField] private float grabDistance = 2f;
    [SerializeField] private Vector3 holdOffset = new Vector3(0, 1, 1);

    private IGrabbable heldObject;
    private Transform holdPoint;
    private ulong heldObjectNetId;
    private PlayerInput playerInput;

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
            // Hide highlight when not holding anything
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
            UpdatePreview();

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

    void TryGrab()
    {
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
            heldObjectNetId = objectId;
        }
    }

    void UpdatePreview()
    {
        PlaceableObject placeableObj = heldObject as PlaceableObject;
        if (placeableObj == null || LevelGrid.Instance == null) return;

        Vector2Int gridPos = GetPlacementGridPosition();
        bool canPlace = LevelGrid.Instance.CanPlaceAt(gridPos);

        // Update object preview
        placeableObj.SetPlacementPreview(canPlace);

        // Show grid highlight
        LevelGrid.Instance.ShowHighlight(gridPos, canPlace);
    }

    void TryPlace()
    {
        if (LevelGrid.Instance == null) return;

        Vector2Int gridPos = GetPlacementGridPosition();

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

    Vector2Int GetPlacementGridPosition()
    {
        if (LevelGrid.Instance == null) return Vector2Int.zero;

        Vector3 placementPos = transform.position + transform.forward * LevelGrid.Instance.TileSize;
        return LevelGrid.Instance.WorldToGrid(placementPos);
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
        heldObjectNetId = 0;

        // Hide highlight when clearing held object
        if (LevelGrid.Instance != null)
        {
            LevelGrid.Instance.HideHighlight();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabDistance);
    }
}