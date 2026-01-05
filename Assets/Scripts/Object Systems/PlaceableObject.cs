using UnityEngine;
using Unity.Netcode;

public class PlaceableObject : NetworkBehaviour, IGrabbable
{
    [Header("Physics")]
    [SerializeField] private float holdForce = 300f;
    [SerializeField] private float holdDrag = 10f;

    [Header("Grid Settings")]
    [SerializeField] private GridLayer objectLayer = GridLayer.Foundation; // Which layer this object belongs to

    [Header("Materials")]
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;
    [SerializeField] private Material placedMaterial;
    [SerializeField] private Material lockedMaterial;

    // Network state
    private NetworkVariable<bool> isBeingHeld = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isPlaced = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isLocked = new NetworkVariable<bool>(false);

    public Vector2Int GridPosition { get; private set; }
    public GridLayer Layer => objectLayer; // Public getter for layer
    public bool IsLocked => isLocked.Value;
    public bool IsPlaced => isPlaced.Value;

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
        isLocked.OnValueChanged += OnLockedStateChanged;
        
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
        isLocked.OnValueChanged -= OnLockedStateChanged;
    }

    void OnStateChanged(bool oldValue, bool newValue)
    {
        // Update client physics when state changes
        if (!IsServer)
        {
            UpdateClientPhysicsState();
        }
        
        // Update visuals for all clients
        UpdateVisuals();
    }

    void OnLockedStateChanged(bool oldValue, bool newValue)
    {
        // Update visuals when lock state changes
        UpdateVisuals();
        
        // Update client physics
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
                // Being held: kinematic, no collisions (prevents pushing player)
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
            else if (isPlaced.Value)
            {
                // Placed: kinematic, but KEEP collisions (so player can interact)
                rb.isKinematic = true;
                rb.detectCollisions = true;
            }
            else
            {
                // Free/dropped: normal physics with collisions
                rb.isKinematic = false;
                rb.detectCollisions = true;
            }
        }
    }

    void UpdateVisuals()
    {
        if (rend == null) return;
        
        // Determine which material to show based on network state
        if (isLocked.Value && lockedMaterial != null)
        {
            rend.material = lockedMaterial;
        }
        else if (isPlaced.Value && placedMaterial != null)
        {
            rend.material = placedMaterial;
        }
        else
        {
            rend.material = originalMaterial;
        }
    }

    void FixedUpdate()
    {
        // Only simulate physics on server
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

    public bool CanBeGrabbed() => !isBeingHeld.Value && !isLocked.Value;

    public void OnGrabbed(Transform newHoldPoint)
    {
        if (!IsServer || isLocked.Value) return;

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

        // Visual update happens automatically via OnStateChanged
    }

    public void OnPlaced(Vector2Int gridPos)
    {
        if (!IsServer) return;

        isBeingHeld.Value = false;
        isPlaced.Value = true;
        holdPoint = null;
        GridPosition = gridPos;

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

        // Register on the appropriate layer
        LevelGrid.Instance?.Register(gridPos, objectLayer, this);
        
        // Tell all clients to register this object
        RegisterOnClientsClientRpc(gridPos, objectLayer);
    }

    [ClientRpc]
    void RegisterOnClientsClientRpc(Vector2Int gridPos, GridLayer layer)
    {
        if (IsServer) return; // Server already registered
        
        GridPosition = gridPos;
        LevelGrid.Instance?.Register(gridPos, layer, this);
    }

    [ClientRpc]
    void UnregisterOnClientsClientRpc(Vector2Int gridPos, GridLayer layer)
    {
        if (IsServer) return; // Server already unregistered
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

    #region Tool Interactions

    public void LockInPlace()
    {
        if (!IsServer) return;
        isLocked.Value = true;
        // Visual update happens automatically via OnLockedStateChanged
    }

    public void Unlock()
    {
        if (!IsServer) return;
        isLocked.Value = false;
        // Visual update happens automatically via OnLockedStateChanged
    }

    #endregion

    #region Placement Validation

    public bool CanPlaceAtPosition(Vector2Int gridPos)
    {
        if (LevelGrid.Instance == null) return false;

        // Check if this layer is available at the position
        if (!LevelGrid.Instance.CanPlaceAt(gridPos, objectLayer))
            return false;

        // Walls and Objects require a foundation
        if (objectLayer == GridLayer.Wall || objectLayer == GridLayer.Object)
        {
            if (!LevelGrid.Instance.HasFoundation(gridPos))
            {
                Debug.Log($"Cannot place {objectLayer} without foundation at {gridPos}");
                return false;
            }
        }

        return true;
    }

    #endregion
}