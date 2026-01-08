using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Debug tool that damages objects when used
/// Press F on any Refined object to damage it for testing repair
/// </summary>
public class DamageTool : NetworkBehaviour, IGrabbable, ITool
{
    [Header("Physics")]
    [SerializeField] private float holdForce = 300f;
    [SerializeField] private float holdDrag = 10f;

    [Header("Tool Settings")]
    [SerializeField] private string toolName = "Damage Tool (DEBUG)";
    [SerializeField] private float damageHoldTime = 1f; // 1 second to damage
    
    public float DamageHoldTime => damageHoldTime;

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

        Debug.Log($"[DamageTool] Using damage tool");

        // Get grid position
        Vector2Int gridPos = LevelGrid.Instance.WorldToGrid(target.transform.position);
        
        // Find all objects at this position
        var allObjects = LevelGrid.Instance.GetAllObjectsAt(gridPos);
        
        if (allObjects.Count == 0)
        {
            Debug.Log($"[DamageTool] No objects found at {gridPos}");
            return;
        }

        // Damage the first Refined object we find (prioritize Object > Wall > Foundation)
        PlaceableObject targetObject = null;
        
        // Try Object layer first
        foreach (var obj in allObjects)
        {
            if (obj.Layer == GridLayer.Object && obj.State == ObjectState.Refined)
            {
                targetObject = obj;
                break;
            }
        }
        
        // Try Wall layer
        if (targetObject == null)
        {
            foreach (var obj in allObjects)
            {
                if (obj.Layer == GridLayer.Wall && obj.State == ObjectState.Refined)
                {
                    targetObject = obj;
                    break;
                }
            }
        }
        
        // Try Foundation layer
        if (targetObject == null)
        {
            foreach (var obj in allObjects)
            {
                if (obj.Layer == GridLayer.Foundation && obj.State == ObjectState.Refined)
                {
                    targetObject = obj;
                    break;
                }
            }
        }

        if (targetObject == null)
        {
            Debug.Log($"[DamageTool] No Refined objects to damage at {gridPos}");
            return;
        }

        // DAMAGE IT!
        Debug.Log($"[DamageTool] Damaging {targetObject.gameObject.name} at {gridPos} (Layer: {targetObject.Layer})");
        targetObject.Damage();
        
        DamageEffectClientRpc(targetObject.GetComponent<NetworkObject>().NetworkObjectId);
    }
    
    [ClientRpc]
    void DamageEffectClientRpc(ulong objectId)
    {
        // Optional: Add visual/audio feedback for damage
        Debug.Log($"[DamageTool CLIENT] Object damaged!");
    }

    public bool CanBeUsed() => isBeingHeld.Value;

    public string GetToolName() => toolName;

    #endregion
}