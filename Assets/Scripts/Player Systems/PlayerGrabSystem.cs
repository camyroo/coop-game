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
        
        if (heldObject != null)
        {
            return;
        }
        
        GrabObjectServerRpc(objectId);
    }

    public bool CanInteractWithStation()
    {
        IGrabbable nearbyGrabbable = FindNearestGrabbable();
        return nearbyGrabbable == null && heldObject == null;
    }

    Vector2Int GetTargetGridPosition()
    {
        if (LevelGrid.Instance == null) return Vector2Int.zero;
        if (holdPoint == null) return Vector2Int.zero;

        return LevelGrid.Instance.WorldToGrid(holdPoint.position);
    }

    void UpdatePlacementPreview()
    {
        if (LevelGrid.Instance == null) return;

        Vector2Int gridPos = GetTargetGridPosition();
        
        if (!LevelGrid.Instance.IsInBounds(gridPos))
        {
            // Try PlaceableObject
            PlaceableObject placeableObj = heldObject as PlaceableObject;
            if (placeableObj != null)
            {
                placeableObj.SetPlacementPreview(false);
            }
            
            // Try RawMaterial
            RawMaterial rawMaterial = heldObject as RawMaterial;
            if (rawMaterial != null)
            {
                rawMaterial.SetPlacementPreview(false);
            }
            
            LevelGrid.Instance.HideHighlight();
            return;
        }
        
        // Check what type of object we're holding
        PlaceableObject placeable = heldObject as PlaceableObject;
        RawMaterial raw = heldObject as RawMaterial;
        
        bool canPlace = false;
        
        if (placeable != null)
        {
            // PlaceableObject placement rules
            canPlace = placeable.CanPlaceAtPosition(gridPos);
            placeable.SetPlacementPreview(canPlace);
        }
        else if (raw != null)
        {
            // RawMaterial placement rules - can always stack on same layer
            canPlace = true; // Raw materials can stack
            raw.SetPlacementPreview(canPlace);
        }
        
        LevelGrid.Instance.ShowHighlight(gridPos, canPlace);
    }

    void UpdateToolPreview()
    {
        if (LevelGrid.Instance == null) return;

        Vector2Int gridPos = GetTargetGridPosition();
        
        // Check for placed objects OR raw materials
        List<PlaceableObject> objectsAtPos = LevelGrid.Instance.GetAllObjectsAt(gridPos);
        
        // Also check for raw materials
        bool hasRawMaterials = LevelGrid.Instance.HasRawMaterials(gridPos, GridLayer.Foundation) ||
                               LevelGrid.Instance.HasRawMaterials(gridPos, GridLayer.Wall) ||
                               LevelGrid.Instance.HasRawMaterials(gridPos, GridLayer.Object);
        
        if (objectsAtPos.Count > 0 || hasRawMaterials)
        {
            LevelGrid.Instance.ShowToolHighlight(gridPos, true);
        }
        else
        {
            LevelGrid.Instance.HideHighlight();
        }
    }

    void TryGrab()
    {
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

        // Prioritize by layer if multiple at same position
        List<IGrabbable> samePositionObjects = new List<IGrabbable>();
        
        foreach (Collider col in nearbyObjects)
        {
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (Mathf.Abs(dist - closestDist) < 0.5f)
            {
                IGrabbable grabbable = col.GetComponent<IGrabbable>();
                if (grabbable != null)
                {
                    samePositionObjects.Add(grabbable);
                }
            }
        }

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
        
        // Check for raw materials first (new priority for recipe system)
        bool hasRawMaterials = LevelGrid.Instance.HasRawMaterials(gridPos, GridLayer.Foundation) ||
                               LevelGrid.Instance.HasRawMaterials(gridPos, GridLayer.Wall) ||
                               LevelGrid.Instance.HasRawMaterials(gridPos, GridLayer.Object);
        
        if (hasRawMaterials)
        {
            // Target the cell itself (for recipe processing)
            Vector3 cellCenter = LevelGrid.Instance.GridToWorld(gridPos, 0);
            GameObject dummyTarget = new GameObject("TempTarget");
            dummyTarget.transform.position = cellCenter;
            
            NetworkObject tempNetObj = dummyTarget.AddComponent<NetworkObject>();
            tempNetObj.Spawn();
            
            UseToolServerRpc(heldObjectNetId, tempNetObj.NetworkObjectId);
            
            // Clean up temp object after a frame
            Destroy(dummyTarget, 0.1f);
            return;
        }
        
        // Check for placed objects (original behavior)
        List<PlaceableObject> objectsAtPos = LevelGrid.Instance.GetAllObjectsAt(gridPos);
        PlaceableObject targetObject = null;
        
        foreach (PlaceableObject obj in objectsAtPos)
        {
            if (obj.IsPlaced && obj.Layer == GridLayer.Object)
            {
                targetObject = obj;
                break;
            }
        }
        
        if (targetObject == null)
        {
            foreach (PlaceableObject obj in objectsAtPos)
            {
                if (obj.IsPlaced && obj.Layer == GridLayer.Wall)
                {
                    targetObject = obj;
                    break;
                }
            }
        }
        
        if (targetObject == null)
        {
            foreach (PlaceableObject obj in objectsAtPos)
            {
                if (obj.IsPlaced && obj.Layer == GridLayer.Foundation)
                {
                    targetObject = obj;
                    break;
                }
            }
        }
        
        if (targetObject != null)
        {
            NetworkObject targetNetObj = targetObject.GetComponent<NetworkObject>();
            if (targetNetObj != null)
            {
                UseToolServerRpc(heldObjectNetId, targetNetObj.NetworkObjectId);
            }
        }
    }

    [ServerRpc]
    void UseToolServerRpc(ulong toolId, ulong targetId)
    {
        Debug.Log($"[SERVER] UseToolServerRpc received: tool={toolId}, target={targetId}");
        
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

        // Check if holding PlaceableObject
        PlaceableObject placeableObj = heldObject as PlaceableObject;
        if (placeableObj != null)
        {
            if (placeableObj.CanPlaceAtPosition(gridPos))
            {
                PlaceObjectServerRpc(heldObjectNetId, gridPos);
            }
            return;
        }

        // Check if holding RawMaterial
        RawMaterial rawMaterial = heldObject as RawMaterial;
        if (rawMaterial != null)
        {
            // Raw materials can always be placed (they stack)
            if (LevelGrid.Instance.IsInBounds(gridPos))
            {
                PlaceObjectServerRpc(heldObjectNetId, gridPos);
            }
            return;
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