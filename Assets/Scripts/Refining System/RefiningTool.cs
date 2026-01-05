using UnityEngine;
using Unity.Netcode;

public class RefiningTool : NetworkBehaviour, IGrabbable, ITool
{
    [Header("Physics")]
    [SerializeField] private float holdForce = 300f;
    [SerializeField] private float holdDrag = 10f;

    [Header("Tool Settings")]
    [SerializeField] private string toolName = "Refining Tool";
    [SerializeField] private float useRange = 3f;

    private NetworkVariable<bool> isBeingHeld = new NetworkVariable<bool>(false);
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
        isBeingHeld.OnValueChanged += OnHeldStateChanged;
        
        // Initial setup for clients
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
        // Tools can't be placed on grid - just drop them instead
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

        Debug.Log($"[SERVER] Tool OnUse called on target: {target.name}");

        PlaceableObject placeableObj = target.GetComponent<PlaceableObject>();
        if (placeableObj != null)
        {
            Debug.Log($"[SERVER] PlaceableObject found. IsLocked: {placeableObj.IsLocked}, IsPlaced: {placeableObj.IsPlaced}");
            
            // Toggle lock state
            if (placeableObj.IsLocked)
            {
                placeableObj.Unlock();
                Debug.Log($"[SERVER] Unlocked {target.name}");
            }
            else if (placeableObj.IsPlaced)
            {
                placeableObj.LockInPlace();
                Debug.Log($"[SERVER] Locked {target.name}");
            }
            else
            {
                Debug.Log($"[SERVER] Object is not placed, cannot lock/unlock");
            }
        }
        else
        {
            Debug.LogError($"[SERVER] No PlaceableObject component found on {target.name}");
        }
    }

    public bool CanBeUsed() => isBeingHeld.Value;

    public string GetToolName() => toolName;

    #endregion
}