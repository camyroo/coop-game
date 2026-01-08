using UnityEngine;
using Unity.Netcode;

public class PlaceableObject : NetworkBehaviour, IGrabbable
{
    [Header("Physics")]
    [SerializeField] private float holdForce = 300f;
    [SerializeField] private float holdDrag = 10f;

    [Header("Grid Settings")]
    [SerializeField] private GridLayer objectLayer = GridLayer.Foundation;

    [Header("Processing Settings")]
    [SerializeField] private string requiredToolType = "refining"; // Which tool type can process this object
    
    [Header("Materials")]
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;
    [SerializeField] private Material placedMaterial;
    
    [Header("State Materials")]
    [SerializeField] private Material rawMaterial;      // Visual for RawMaterial state
    [SerializeField] private Material refinedMaterial;  // Visual for Refined state
    [SerializeField] private Material damagedMaterial;  // Visual for Damaged state

    // Network state
    private NetworkVariable<bool> isBeingHeld = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isPlaced = new NetworkVariable<bool>(false);
    private NetworkVariable<ObjectState> objectState = new NetworkVariable<ObjectState>(ObjectState.RawMaterial);

    public Vector2Int GridPosition { get; private set; }
    public GridLayer Layer => objectLayer;
    public ObjectState State => objectState.Value;
    public bool IsPlaced => isPlaced.Value;
    public string RequiredToolType => requiredToolType;
    
    // Backwards compatibility - "locked" now means "refined"
    public bool IsLocked => objectState.Value == ObjectState.Refined;

    private Rigidbody rb;
    private Renderer rend;
    private Material originalMaterial;
    private Transform holdPoint;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rend = GetComponentInChildren<Renderer>();
        if (rend != null) originalMaterial = rend.material;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to state changes
        isBeingHeld.OnValueChanged += OnStateChanged;
        isPlaced.OnValueChanged += OnStateChanged;
        objectState.OnValueChanged += OnObjectStateChanged;
        
        // Initial setup for clients
        if (!IsServer && rb != null)
        {
            UpdateClientPhysicsState();
        }
        
        // Apply initial visuals for all clients
        UpdateVisuals();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        isBeingHeld.OnValueChanged -= OnStateChanged;
        isPlaced.OnValueChanged -= OnStateChanged;
        objectState.OnValueChanged -= OnObjectStateChanged;
    }

    void OnStateChanged(bool oldValue, bool newValue)
    {
        if (!IsServer)
        {
            UpdateClientPhysicsState();
        }
        UpdateVisuals();
    }

    void OnObjectStateChanged(ObjectState oldValue, ObjectState newValue)
    {
        Debug.Log($"[{(IsServer ? "SERVER" : "CLIENT")}] Object state changed: {oldValue} → {newValue}");
        UpdateVisuals();
        
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
        
        // Priority: Being held > State-specific materials
        if (isBeingHeld.Value)
        {
            // Keep original material when being held
            rend.material = originalMaterial;
        }
        else if (isPlaced.Value)
        {
            // Show state-specific material when placed
            switch (objectState.Value)
            {
                case ObjectState.RawMaterial:
                    rend.material = rawMaterial != null ? rawMaterial : placedMaterial;
                    break;
                    
                case ObjectState.Refined:
                    rend.material = refinedMaterial != null ? refinedMaterial : placedMaterial;
                    break;
                    
                case ObjectState.Damaged:
                    rend.material = damagedMaterial != null ? damagedMaterial : placedMaterial;
                    break;
            }
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

    public bool CanBeGrabbed()
    {
        // Can't grab if being held or if refined
        return !isBeingHeld.Value && objectState.Value != ObjectState.Refined;
    }

    public void OnGrabbed(Transform newHoldPoint)
    {
        if (!IsServer || objectState.Value == ObjectState.Refined) return;

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
            LevelGrid.Instance?.Unregister(GridPosition, objectLayer);
            UnregisterOnClientsClientRpc(GridPosition, objectLayer);
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

        isBeingHeld.Value = false;
        isPlaced.Value = true;
        holdPoint = null;
        GridPosition = gridPos;

        // Reset to RawMaterial state when placed
        objectState.Value = ObjectState.RawMaterial;

        // Snap to grid
        transform.position = LevelGrid.Instance.GridToWorld(gridPos, transform.position.y);
        transform.rotation = Quaternion.identity;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.excludeLayers = 0;
        }

        LevelGrid.Instance?.Register(gridPos, objectLayer, this);
        RegisterOnClientsClientRpc(gridPos, objectLayer);
    }

    [ClientRpc]
    void RegisterOnClientsClientRpc(Vector2Int gridPos, GridLayer layer)
    {
        if (IsServer) return;
        
        GridPosition = gridPos;
        LevelGrid.Instance?.Register(gridPos, layer, this);
    }

    [ClientRpc]
    void UnregisterOnClientsClientRpc(Vector2Int gridPos, GridLayer layer)
    {
        if (IsServer) return;
        LevelGrid.Instance?.Unregister(gridPos, layer);
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

    #region Tool Interactions & State Management

    /// <summary>
    /// Refine object: RawMaterial → Refined
    /// </summary>
    public void Refine()
    {
        if (!IsServer) return;
        if (objectState.Value != ObjectState.RawMaterial) return;
        
        objectState.Value = ObjectState.Refined;
        Debug.Log($"[SERVER] Refined object at {GridPosition}");
    }

    /// <summary>
    /// Damage object: Refined → Damaged
    /// </summary>
    public void Damage()
    {
        if (!IsServer) return;
        if (objectState.Value != ObjectState.Refined) return;
        
        objectState.Value = ObjectState.Damaged;
        Debug.Log($"[SERVER] Damaged object at {GridPosition}");
    }

    /// <summary>
    /// Repair object: Damaged → Refined
    /// </summary>
    public void Repair()
    {
        if (!IsServer) return;
        if (objectState.Value != ObjectState.Damaged) return;
        
        objectState.Value = ObjectState.Refined;
        Debug.Log($"[SERVER] Repaired object at {GridPosition}");
    }

    /// <summary>
    /// Un-refine object (for testing): Refined → RawMaterial
    /// </summary>
    public void Unrefine()
    {
        if (!IsServer) return;
        if (objectState.Value != ObjectState.Refined) return;
        
        objectState.Value = ObjectState.RawMaterial;
        Debug.Log($"[SERVER] Un-refined object at {GridPosition}");
    }

    // Backwards compatibility methods
    public void LockInPlace() => Refine();
    public void Unlock() => Unrefine();

    #endregion

    #region Placement Validation

    public bool CanPlaceAtPosition(Vector2Int gridPos)
    {
        if (LevelGrid.Instance == null) return false;

        if (!LevelGrid.Instance.CanPlaceAt(gridPos, objectLayer))
        {
            return false;
        }

        // Walls and Objects require a REFINED foundation (not just placed)
        if (objectLayer == GridLayer.Wall || objectLayer == GridLayer.Object)
        {
            PlaceableObject foundation = LevelGrid.Instance.GetObjectAt(gridPos, GridLayer.Foundation);
            
            if (foundation == null)
            {
                return false;
            }
            
            // Foundation must be refined
            if (foundation.State != ObjectState.Refined)
            {
                return false;
            }
        }

        // Walls and Objects are mutually exclusive
        if (objectLayer == GridLayer.Wall)
        {
            if (LevelGrid.Instance.GetObjectAt(gridPos, GridLayer.Object) != null)
            {
                return false;
            }
        }
        else if (objectLayer == GridLayer.Object)
        {
            if (LevelGrid.Instance.GetObjectAt(gridPos, GridLayer.Wall) != null)
            {
                return false;
            }
        }

        return true;
    }

    #endregion
}