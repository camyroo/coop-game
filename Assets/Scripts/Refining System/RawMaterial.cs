using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Raw material object that players place on the grid
/// Auto-detects which layer to place on based on active blueprint
/// </summary>
public class RawMaterial : NetworkBehaviour, IGrabbable
{
    [Header("Material Definition")]
    [SerializeField] private MaterialType materialType = MaterialType.Concrete;
    
    [Header("Physics")]
    [SerializeField] private float holdForce = 300f;
    [SerializeField] private float holdDrag = 10f;

    [Header("Materials")]
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;
    [SerializeField] private Material defaultMaterial;

    // Network state
    private NetworkVariable<bool> isBeingHeld = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isPlaced = new NetworkVariable<bool>(false);

    public Vector2Int GridPosition { get; private set; }
    public GridLayer Layer { get; private set; } // Auto-detected from blueprint
    public MaterialType MaterialType => materialType;
    public bool IsPlaced => isPlaced.Value;

    private Rigidbody rb;
    private Renderer rend;
    private Material originalMaterial;
    private Transform holdPoint;
    private float stackHeight = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rend = GetComponentInChildren<Renderer>();
        if (rend != null) originalMaterial = rend.material;
    }
    
    void OnDestroy()
    {
        // Clean up grid registration when destroyed
        if (isPlaced.Value && LevelGrid.Instance != null)
        {
            LevelGrid.Instance.UnregisterRawMaterial(GridPosition, Layer, this);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        isBeingHeld.OnValueChanged += OnStateChanged;
        isPlaced.OnValueChanged += OnStateChanged;
        
        if (!IsServer && rb != null)
        {
            UpdateClientPhysicsState();
        }
        
        UpdateVisuals();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        isBeingHeld.OnValueChanged -= OnStateChanged;
        isPlaced.OnValueChanged -= OnStateChanged;
    }

    void OnStateChanged(bool oldValue, bool newValue)
    {
        if (!IsServer)
        {
            UpdateClientPhysicsState();
        }
        UpdateVisuals();
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
            else if (isPlaced.Value)
            {
                rb.isKinematic = true;
                rb.detectCollisions = true;
            }
            else
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
            }
        }
    }

    void UpdateVisuals()
    {
        if (rend == null) return;
        
        if (isBeingHeld.Value)
        {
            rend.material = originalMaterial;
        }
        else if (isPlaced.Value)
        {
            rend.material = defaultMaterial != null ? defaultMaterial : originalMaterial;
        }
        else
        {
            rend.material = originalMaterial;
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

    #region IGrabbable

    public bool CanBeGrabbed() => !isBeingHeld.Value;

    public void OnGrabbed(Transform newHoldPoint)
    {
        if (!IsServer) return;

        isBeingHeld.Value = true;
        isPlaced.Value = false;
        holdPoint = newHoldPoint;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.excludeLayers = LayerMask.GetMask("Player");
            rb.freezeRotation = true;
        }

        if (GridPosition != Vector2Int.zero)
        {
            LevelGrid.Instance?.UnregisterRawMaterial(GridPosition, Layer, this);
            UnregisterOnClientsClientRpc(GridPosition, Layer);
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
            rb.excludeLayers = 0;
            rb.freezeRotation = false;
        }
    }

    public void OnPlaced(Vector2Int gridPos)
    {
        if (!IsServer) return;
        
        // AUTO-DETECT LAYER FROM ACTIVE BLUEPRINT
        CellBlueprint activeBlueprint = LevelGrid.Instance?.GetActiveBlueprint(gridPos);
        
        if (activeBlueprint == null || activeBlueprint.Recipe == null)
        {
            Debug.LogWarning($"[RawMaterial] No active blueprint at {gridPos} in current phase. Cannot place.");
            OnDropped(); // Drop it instead of placing
            return;
        }
        
        // Use the blueprint's target layer
        GridLayer detectedLayer = activeBlueprint.Recipe.TargetLayer;
        Debug.Log($"[RawMaterial] Auto-detected layer: {detectedLayer} from blueprint at {gridPos}");
        
        // Check if this material type matches what the blueprint needs
        MaterialType? neededMaterial = activeBlueprint.Recipe.GetMaterialForTool(GetToolTypeForMaterial());
        if (neededMaterial == null || neededMaterial != materialType)
        {
            Debug.LogWarning($"[RawMaterial] Blueprint at {gridPos} doesn't need {materialType}");
            OnDropped();
            return;
        }

        isBeingHeld.Value = false;
        isPlaced.Value = true;
        holdPoint = null;
        GridPosition = gridPos;
        Layer = detectedLayer; // Set the auto-detected layer

        // Calculate stack height
        int stackCount = LevelGrid.Instance?.GetRawMaterialCount(gridPos, Layer) ?? 0;
        stackHeight = stackCount * 0.5f;

        // Snap to grid with stack offset
        Vector3 basePos = LevelGrid.Instance.GridToWorld(gridPos, 0);
        transform.position = basePos + Vector3.up * stackHeight;
        transform.rotation = Quaternion.identity;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.excludeLayers = 0;
        }

        LevelGrid.Instance?.RegisterRawMaterial(gridPos, Layer, this);
        RegisterOnClientsClientRpc(gridPos, Layer, stackHeight);
    }
    
    /// <summary>
    /// Get the tool type that processes this material
    /// This is a simple mapping - ideally would come from a config
    /// </summary>
    string GetToolTypeForMaterial()
    {
        switch (materialType)
        {
            case MaterialType.Concrete: return "concrete";
            case MaterialType.Steel: return "steel";
            case MaterialType.Wood: return "wood";
            case MaterialType.Brick: return "brick";
            case MaterialType.Glass: return "glass";
            default: return materialType.ToString().ToLower();
        }
    }

    [ClientRpc]
    void RegisterOnClientsClientRpc(Vector2Int gridPos, GridLayer layer, float height)
    {
        if (IsServer) return;
        
        GridPosition = gridPos;
        Layer = layer;
        stackHeight = height;
        
        // Update position on clients
        Vector3 basePos = LevelGrid.Instance.GridToWorld(gridPos, 0);
        transform.position = basePos + Vector3.up * stackHeight;
        
        LevelGrid.Instance?.RegisterRawMaterial(gridPos, layer, this);
    }

    [ClientRpc]
    void UnregisterOnClientsClientRpc(Vector2Int gridPos, GridLayer layer)
    {
        if (IsServer) return;
        LevelGrid.Instance?.UnregisterRawMaterial(gridPos, layer, this);
    }

    #endregion

    #region Visual Preview

    public void SetPlacementPreview(bool canPlace)
    {
        if (rend != null)
        {
            rend.material = canPlace ? validMaterial : invalidMaterial;
        }
    }

    #endregion
    
    public float GetStackHeight() => stackHeight;
}