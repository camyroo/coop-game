using UnityEngine;
using Unity.Netcode;

public class PlaceableObject : NetworkBehaviour, IGrabbable
{
    [Header("Configuration")]
    [SerializeField] private GameConfig config;

    [Header("State")]
    private NetworkVariable<bool> isPlaced = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isBeingHeld = new NetworkVariable<bool>(false);
    public Vector2Int GridPosition { get; private set; }

    [Header("Visual Feedback")]
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;
    [SerializeField] private Material placedMaterial;

    private float holdForce => config != null ? config.objectHoldForce : 500f;
    private float holdDrag => config != null ? config.objectHoldDrag : 10f;

    private Renderer objRenderer;
    private Rigidbody rb;
    private Material originalMaterial;
    private Transform targetHoldPoint;
    
    #region Unity Lifecycle
    
    void Awake()
    {
        objRenderer = GetComponentInChildren<Renderer>();
        rb = GetComponent<Rigidbody>();
        
        if (objRenderer != null)
        {
            originalMaterial = objRenderer.material;
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isBeingHeld.OnValueChanged += OnHeldStateChanged;
        isPlaced.OnValueChanged += OnPlacedStateChanged;
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        isBeingHeld.OnValueChanged -= OnHeldStateChanged;
        isPlaced.OnValueChanged -= OnPlacedStateChanged;
    }
    
    void FixedUpdate()
    {
        if (isBeingHeld.Value && targetHoldPoint != null && rb != null)
        {
            ApplyHoldPhysics();
        }
    }
    
    #endregion
    
    #region IGrabbable Implementation
    
    public void OnGrabbed(Transform holdPoint)
    {
        if (!IsServer) return;
        
        isPlaced.Value = false;
        isBeingHeld.Value = true;
        targetHoldPoint = holdPoint;
        
        ConfigurePhysicsForHold();
        UnregisterFromGrid();
    }
    
    public void OnDropped()
    {
        if (!IsServer) return;
        
        isBeingHeld.Value = false;
        targetHoldPoint = null;
        
        ConfigurePhysicsForDrop();
        RestoreVisuals();
    }
    
    public void OnPlaced(Vector2Int gridPosition)
    {
        if (!IsServer) return;
        
        isPlaced.Value = true;
        isBeingHeld.Value = false;
        targetHoldPoint = null;
        GridPosition = gridPosition;
        
        SnapToGrid(gridPosition);
        ConfigurePhysicsForPlacement();
        ApplyPlacedVisuals();
        RegisterToGrid(gridPosition);
    }
    
    public bool CanBeGrabbed()
    {
        return !isBeingHeld.Value && !isPlaced.Value;
    }
    
    #endregion
    
    #region Visual Feedback
    
    public void SetPlacementPreview(bool canPlace)
    {
        if (objRenderer != null)
        {
            objRenderer.material = canPlace ? validPlacementMaterial : invalidPlacementMaterial;
        }
    }
    
    private void ApplyPlacedVisuals()
    {
        if (objRenderer != null)
        {
            objRenderer.material = placedMaterial != null ? placedMaterial : originalMaterial;
        }
    }
    
    private void RestoreVisuals()
    {
        if (objRenderer != null)
        {
            objRenderer.material = originalMaterial;
        }
    }
    
    #endregion
    
    #region Physics Configuration
    
    private void ConfigurePhysicsForHold()
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearDamping = 0;
            rb.angularDamping = 0.5f;
        }
    }
    
    private void ConfigurePhysicsForDrop()
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
        }
    }
    
    private void ConfigurePhysicsForPlacement()
    {
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    
    private void ApplyHoldPhysics()
    {
        Vector3 forceDirection = targetHoldPoint.position - transform.position;
        rb.AddForce(forceDirection * holdForce);
        rb.linearVelocity *= (1f - holdDrag * Time.fixedDeltaTime);
        rb.angularVelocity *= (1f - holdDrag * Time.fixedDeltaTime);
    }
    
    #endregion
    
    #region Grid Management
    
    private void SnapToGrid(Vector2Int gridPos)
    {
        Vector3 worldPos = LevelGrid.Instance.GridToWorld(gridPos, transform.position.y);
        transform.position = worldPos;
    }
    
    private void RegisterToGrid(Vector2Int gridPos)
    {
        LevelGrid.Instance?.RegisterObject(gridPos, this);
    }
    
    private void UnregisterFromGrid()
    {
        if (GridPosition != Vector2Int.zero)
        {
            LevelGrid.Instance?.UnregisterObject(GridPosition);
        }
    }
    
    #endregion
    
    #region Network Variable Callbacks
    
    private void OnHeldStateChanged(bool oldValue, bool newValue)
    {
        if (newValue && rb != null)
        {
            rb.useGravity = false;
        }
    }
    
    private void OnPlacedStateChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            ApplyPlacedVisuals();
        }
    }
    
    #endregion
}