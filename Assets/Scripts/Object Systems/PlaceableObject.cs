using UnityEngine;
using Unity.Netcode;

public class PlaceableObject : NetworkBehaviour, IGrabbable
{
    [Header("Physics Settings")]
    [SerializeField] private float holdForce = 300f;
    [SerializeField] private float holdDrag = 10f;
    
    [Header("Visual Feedback")]
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;
    [SerializeField] private Material placedMaterial;

    private NetworkVariable<bool> isPlaced = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isBeingHeld = new NetworkVariable<bool>(false);
    
    public Vector2Int GridPosition { get; private set; }

    private Renderer objRenderer;
    private Rigidbody rb;
    private Material originalMaterial;
    private Transform targetHoldPoint;
    
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
            // Simple force-based holding
            Vector3 direction = targetHoldPoint.position - transform.position;
            rb.AddForce(direction * holdForce);
            rb.linearVelocity *= (1f - holdDrag * Time.fixedDeltaTime);
            rb.angularVelocity *= (1f - holdDrag * Time.fixedDeltaTime);
        }
    }
    
    #region IGrabbable Implementation
    
    public void OnGrabbed(Transform holdPoint)
    {
        if (!IsServer) return;
        
        isPlaced.Value = false;
        isBeingHeld.Value = true;
        targetHoldPoint = holdPoint;
        
        // Setup physics for holding
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearDamping = 0;
            rb.angularDamping = 0.5f;
        }
        
        UnregisterFromGrid();
    }
    
    public void OnDropped()
    {
        if (!IsServer) return;
        
        isBeingHeld.Value = false;
        targetHoldPoint = null;
        
        // Restore normal physics
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
        }
        
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
        
        // Lock in place
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
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
    
    #region Grid Management
    
    private void SnapToGrid(Vector2Int gridPos)
    {
        if (LevelGrid.Instance == null) return;
        Vector3 worldPos = LevelGrid.Instance.GridToWorld(gridPos, transform.position.y);
        transform.position = worldPos;
        transform.rotation = Quaternion.identity;
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
    
    #region Network Callbacks
    
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