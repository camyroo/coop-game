using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tool that processes raw materials according to cell blueprints
/// Also handles repairing damaged objects (tool-only, no materials needed for now)
/// </summary>
public class ProcessingTool : NetworkBehaviour, IGrabbable, ITool
{
    [Header("Physics")]
    [SerializeField] private float holdForce = 300f;
    [SerializeField] private float holdDrag = 10f;

    [Header("Tool Settings")]
    [SerializeField] private string toolName = "Processing Tool";
    [SerializeField] private float useRange = 3f;
    [SerializeField] private string toolType = "concrete";

    private NetworkVariable<bool> isBeingHeld = new NetworkVariable<bool>(false);
    private Rigidbody rb;
    private Renderer rend;
    private Material originalMaterial;
    private Transform holdPoint;

    public string ToolType => toolType;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rend = GetComponentInChildren<Renderer>();
        if (rend != null) originalMaterial = rend.material;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        isBeingHeld.OnValueChanged += OnHeldStateChanged;
        
        if (!IsServer && rb != null)
        {
            UpdateClientPhysicsState();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        isBeingHeld.OnValueChanged -= OnHeldStateChanged;
    }

    void OnHeldStateChanged(bool oldValue, bool newValue)
    {
        if (!IsServer)
        {
            UpdateClientPhysicsState();
        }
    }

    void UpdateClientPhysicsState()
    {
        if (!IsServer && rb != null)
        {
            if (isBeingHeld.Value)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
            else
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
            }
        }
    }

    void FixedUpdate()
    {
        if (!IsServer) return;
        
        if (isBeingHeld.Value && holdPoint != null && rb != null)
        {
            Vector3 direction = holdPoint.position - transform.position;
            rb.AddForce(direction * holdForce);
            rb.linearVelocity *= (1f - holdDrag * Time.fixedDeltaTime);
            rb.angularVelocity *= (1f - holdDrag * Time.fixedDeltaTime);
        }
    }

    #region IGrabbable Implementation

    public bool CanBeGrabbed() => !isBeingHeld.Value;

    public void OnGrabbed(Transform newHoldPoint)
    {
        if (!IsServer) return;

        isBeingHeld.Value = true;
        holdPoint = newHoldPoint;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.excludeLayers = LayerMask.GetMask("Player");
            rb.freezeRotation = true;
        }
    }

    public void OnDropped()
    {
        if (!IsServer) return;

        isBeingHeld.Value = false;
        holdPoint = null;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.excludeLayers = 0;
            rb.freezeRotation = false;
        }
    }

    public void OnPlaced(Vector2Int gridPosition)
    {
        OnDropped();
    }

    #endregion

    #region ITool Implementation

    public void OnEquipped(Transform newHoldPoint)
    {
        OnGrabbed(newHoldPoint);
    }

    public void OnUnequipped()
    {
        OnDropped();
    }

    public void OnUse(GameObject target)
    {
        if (!IsServer) return;
        if (LevelGrid.Instance == null) return;

        Vector2Int gridPos = LevelGrid.Instance.WorldToGrid(target.transform.position);
        Debug.Log($"[ProcessingTool] Using {toolName} at {gridPos}");

        // Try to repair damaged object first
        if (TryRepairDamagedObject(gridPos))
        {
            return; // Repair successful
        }

        // Otherwise, process raw materials for blueprint
        ProcessBlueprintMaterials(gridPos);
    }

    bool TryRepairDamagedObject(Vector2Int gridPos)
    {
        // Check all layers for damaged objects
        List<PlaceableObject> allObjects = LevelGrid.Instance.GetAllObjectsAt(gridPos);
        
        foreach (PlaceableObject obj in allObjects)
        {
            if (obj.State == ObjectState.Damaged)
            {
                // TODO: Check if this tool matches the object's original recipe
                // For now, just repair any damaged object
                
                Debug.Log($"[ProcessingTool] Repairing damaged object at {gridPos}");
                obj.Repair();
                return true;
            }
        }
        
        return false;
    }

    void ProcessBlueprintMaterials(Vector2Int gridPos)
    {
        // Get active blueprint for current phase
        CellBlueprint blueprint = LevelGrid.Instance.GetActiveBlueprint(gridPos);
        
        if (blueprint == null)
        {
            Debug.Log($"[ProcessingTool] No active blueprint at {gridPos}");
            return;
        }

        if (blueprint.IsComplete)
        {
            Debug.Log($"[ProcessingTool] Blueprint already complete at {gridPos}");
            return;
        }

        // Check if this tool is accepted by the blueprint
        if (!blueprint.AcceptsTool(toolType))
        {
            Debug.Log($"[ProcessingTool] Blueprint doesn't accept tool type '{toolType}'");
            return;
        }

        // Get the material type this tool processes
        MaterialType? materialType = blueprint.Recipe.GetMaterialForTool(toolType);
        if (materialType == null)
        {
            Debug.LogError($"[ProcessingTool] Tool '{toolType}' has no matching material in recipe");
            return;
        }

        // Find raw materials at this cell that match what we need
        List<RawMaterial> rawMaterials = LevelGrid.Instance.GetRawMaterials(gridPos, blueprint.Recipe.TargetLayer);
        RawMaterial targetMaterial = rawMaterials.FirstOrDefault(m => m.MaterialType == materialType.Value);

        if (targetMaterial == null)
        {
            Debug.Log($"[ProcessingTool] No {materialType.Value} raw material at {gridPos} on {blueprint.Recipe.TargetLayer} layer");
            return;
        }

        // Process the material through the blueprint
        bool processed = blueprint.ProcessMaterial(toolType, materialType.Value);

        if (processed)
        {
            // Unregister from grid BEFORE destroying
            LevelGrid.Instance.UnregisterRawMaterial(gridPos, blueprint.Recipe.TargetLayer, targetMaterial);
            
            // Destroy the raw material object
            NetworkObject netObj = targetMaterial.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(targetMaterial.gameObject);
            }

            Debug.Log($"[ProcessingTool] Processed and destroyed {materialType.Value} at {gridPos}");
        }
    }

    public bool CanBeUsed() => isBeingHeld.Value;

    public string GetToolName() => toolName;

    #endregion
}