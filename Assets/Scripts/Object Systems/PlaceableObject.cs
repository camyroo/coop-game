using UnityEngine;
using Unity.Netcode;

public class PlaceableObject : NetworkBehaviour, IGrabbable
{
    [Header("Physics")]
    [SerializeField] private float holdForce = 300f;
    [SerializeField] private float holdDrag = 10f;

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
        
        // Initial setup for clients
        if (!IsServer && rb != null)
        {
            UpdateClientPhysicsState();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        isBeingHeld.OnValueChanged -= OnStateChanged;
        isPlaced.OnValueChanged -= OnStateChanged;
    }

    void OnStateChanged(bool oldValue, bool newValue)
    {
        // Update client physics when state changes
        if (!IsServer)
        {
            UpdateClientPhysicsState();
        }
    }

    void UpdateClientPhysicsState()
    {
        if (!IsServer && rb != null)
        {
            // Only make kinematic if the object is being held or placed
            if (isBeingHeld.Value || isPlaced.Value)
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
            LevelGrid.Instance?.Unregister(GridPosition);
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

        if (rend != null) rend.material = originalMaterial;
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

        if (rend != null && placedMaterial != null)
        {
            rend.material = placedMaterial;
        }

        LevelGrid.Instance?.Register(gridPos, this);
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
        if (rend != null && lockedMaterial != null)
        {
            rend.material = lockedMaterial;
        }
    }

    public void Unlock()
    {
        if (!IsServer) return;
        isLocked.Value = false;
        if (rend != null && placedMaterial != null)
        {
            rend.material = placedMaterial;
        }
    }

    #endregion
}