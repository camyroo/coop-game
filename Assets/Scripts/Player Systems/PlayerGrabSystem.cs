using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

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
        
        // Use the new layer-aware validation
        bool canPlace = placeableObj.CanPlaceAtPosition(gridPos);
        placeableObj.SetPlacementPreview(canPlace);
        LevelGrid.Instance.ShowHighlight(gridPos, canPlace);
    }

    void UpdateToolPreview()
    {
        if (LevelGrid.Instance == null) return;

        Vector2Int gridPos = GetTargetGridPosition();
        
        // Tools can target any placed object on any layer
        // Check all layers for placed objects
        List<PlaceableObject> objectsAtPos = LevelGrid.Instance.GetAllObjectsAt(gridPos);
        
        PlaceableObject targetObject = null;
        foreach (PlaceableObject obj in objectsAtPos)
        {
            if (obj.IsPlaced)
            {
                targetObject = obj;
                break; // Found a valid target
            }
        }
        
        if (targetObject != null)
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

        // Group objects by distance
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

        // Check if there are multiple objects at the same position
        // Prioritize: Object > Wall > Foundation
        List<IGrabbable> samePositionObjects = new List<IGrabbable>();
        
        foreach (Collider col in nearbyObjects)
        {
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (Mathf.Abs(dist - closestDist) < 0.5f) // Same position tolerance
            {
                IGrabbable grabbable = col.GetComponent<IGrabbable>();
                if (grabbable != null)
                {
                    samePositionObjects.Add(grabbable);
                }
            }
        }

        // If multiple objects at same position, prioritize by layer
        if (samePositionObjects.Count > 1)
        {
            // Try Object layer first
            foreach (IGrabbable grab in samePositionObjects)
            {
                PlaceableObject obj = grab as PlaceableObject;
                if (obj != null && obj.Layer == GridLayer.Object && obj.CanBeGrabbed())
                {
                    return grab;
                }
            }
            
            // Then Wall layer
            foreach (IGrabbable grab in samePositionObjects)
            {
                PlaceableObject obj = grab as PlaceableObject;
                if (obj != null && obj.Layer == GridLayer.Wall && obj.CanBeGrabbed())
                {
                    return grab;
                }
            }
            
            // Finally Foundation layer
            foreach (IGrabbable grab in samePositionObjects)
            {
                PlaceableObject obj = grab as PlaceableObject;
                if (obj != null && obj.Layer == GridLayer.Foundation && obj.CanBeGrabbed())
                {
                    return grab;
                }
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
        Debug.Log($"[CLIENT] Tool targeting grid position: {gridPos}");
        
        // Check all layers for a placed object to target
        List<PlaceableObject> objectsAtPos = LevelGrid.Instance.GetAllObjectsAt(gridPos);
        
        // Prioritize layers: Object > Wall > Foundation
        PlaceableObject targetObject = null;
        
        // First try Object layer
        foreach (PlaceableObject obj in objectsAtPos)
        {
            if (obj.IsPlaced && obj.Layer == GridLayer.Object)
            {
                targetObject = obj;
                Debug.Log($"[CLIENT] Found {obj.Layer} object at grid position (priority)");
                break;
            }
        }
        
        // Then try Wall layer
        if (targetObject == null)
        {
            foreach (PlaceableObject obj in objectsAtPos)
            {
                if (obj.IsPlaced && obj.Layer == GridLayer.Wall)
                {
                    targetObject = obj;
                    Debug.Log($"[CLIENT] Found {obj.Layer} object at grid position (priority)");
                    break;
                }
            }
        }
        
        // Finally try Foundation layer
        if (targetObject == null)
        {
            foreach (PlaceableObject obj in objectsAtPos)
            {
                if (obj.IsPlaced && obj.Layer == GridLayer.Foundation)
                {
                    targetObject = obj;
                    Debug.Log($"[CLIENT] Found {obj.Layer} object at grid position");
                    break;
                }
            }
        }
        
        if (targetObject != null)
        {
            NetworkObject targetNetObj = targetObject.GetComponent<NetworkObject>();
            if (targetNetObj != null)
            {
                Debug.Log($"[CLIENT] Sending UseToolServerRpc with IDs: tool={heldObjectNetId}, target={targetNetObj.NetworkObjectId}");
                UseToolServerRpc(heldObjectNetId, targetNetObj.NetworkObjectId);
            }
            else
            {
                Debug.LogError($"[CLIENT] Object has no NetworkObject component!");
            }
        }
        else
        {
            Debug.Log($"[CLIENT] No placed object found at grid position {gridPos}");
        }
    }
    [ServerRpc]
    void UseToolServerRpc(ulong toolId, ulong targetId)
    {
        Debug.Log($"[SERVER] UseToolServerRpc received: tool={toolId}, target={targetId}");
        
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(toolId, out NetworkObject toolObj) &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObj))
        {
            Debug.Log($"[SERVER] Found both tool and target objects");
            ITool tool = toolObj.GetComponent<ITool>();
            if (tool != null && tool.CanBeUsed())
            {
                Debug.Log($"[SERVER] Calling tool.OnUse()");
                tool.OnUse(targetObj.gameObject);
            }
            else
            {
                Debug.LogError($"[SERVER] Tool is null or can't be used");
            }
        }
        else
        {
            Debug.LogError($"[SERVER] Could not find tool or target in spawned objects");
        }
    }

    void TryPlace()
    {
        if (LevelGrid.Instance == null) return;

        PlaceableObject placeableObj = heldObject as PlaceableObject;
        if (placeableObj == null) return;

        Vector2Int gridPos = GetTargetGridPosition();

        // Use the object's CanPlaceAtPosition which handles layer checking internally
        if (placeableObj.CanPlaceAtPosition(gridPos))
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