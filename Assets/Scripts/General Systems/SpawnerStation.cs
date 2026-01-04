using UnityEngine;
using Unity.Netcode;

public class SpawnerStation : NetworkBehaviour, IInteractable
{
    [Header("Spawner Settings")]
    [SerializeField] private GameObject prefabToSpawn;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private string itemName = "Object";
    
    [Header("Spawn Limits")]
    [SerializeField] private bool hasSpawnLimit = false;
    [SerializeField] private int maxSpawns = 10;
    
    [Header("Visual Feedback")]
    [SerializeField] private Renderer stationRenderer;
    [SerializeField] private Color availableColor = Color.green;
    [SerializeField] private Color unavailableColor = Color.red;
    
    private NetworkVariable<int> spawnCount = new NetworkVariable<int>(0);

    void Start()
    {
        if (spawnPoint == null)
        {
            GameObject spawnPointObj = new GameObject("SpawnPoint");
            spawnPoint = spawnPointObj.transform;
            spawnPoint.SetParent(transform);
            spawnPoint.localPosition = Vector3.up * 2f;
        }
        
        UpdateVisuals();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        spawnCount.OnValueChanged += OnSpawnCountChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        spawnCount.OnValueChanged -= OnSpawnCountChanged;
    }

    #region IInteractable Implementation

    public void Interact(PlayerController player)
    {
        if (!IsServer) return;
        if (!CanSpawn()) return;

        PlayerGrabSystem grabSystem = player.GetComponent<PlayerGrabSystem>();
        if (grabSystem == null)
        {
            Debug.LogError("Player doesn't have PlayerGrabSystem!");
            return;
        }

        // Spawn directly at the hold point
        GameObject spawnedObj = Instantiate(prefabToSpawn, grabSystem.HoldPoint.position, Quaternion.identity);
        NetworkObject netObj = spawnedObj.GetComponent<NetworkObject>();
        
        if (netObj != null)
        {
            netObj.Spawn();
            spawnCount.Value++;
            
            // Give it to the player immediately
            GiveToPlayerClientRpc(player.OwnerClientId, netObj.NetworkObjectId);
        }
        else
        {
            Debug.LogError($"Prefab {prefabToSpawn.name} needs NetworkObject component!");
            Destroy(spawnedObj);
        }
    }

    [ClientRpc]
    void GiveToPlayerClientRpc(ulong playerClientId, ulong objectId)
    {
        // Only the specific player should grab it
        if (NetworkManager.Singleton.LocalClientId != playerClientId) return;

        // Small delay to ensure network spawn completes
        StartCoroutine(GrabAfterDelay(objectId));
    }

    System.Collections.IEnumerator GrabAfterDelay(ulong objectId)
    {
        yield return new WaitForSeconds(0.1f);

        // Find the local player's grab system
        foreach (var playerObj in FindObjectsByType<PlayerGrabSystem>(FindObjectsSortMode.None))
        {
            if (playerObj.IsOwner)
            {
                playerObj.GrabSpecificObject(objectId);
                break;
            }
        }
    }

    public bool CanInteract(PlayerController player)
    {
        if (!CanSpawn()) return false;
        
        // Check if player is already holding something
        PlayerGrabSystem grabSystem = player.GetComponent<PlayerGrabSystem>();
        if (grabSystem != null && grabSystem.IsHoldingObject)
        {
            return false;
        }
        
        return true;
    }

    public string GetInteractionPrompt()
    {
        if (CanSpawn())
        {
            return $"[F] Spawn {itemName}";
        }
        return "Spawn limit reached";
    }

    #endregion

    bool CanSpawn()
    {
        if (!hasSpawnLimit) return true;
        return spawnCount.Value < maxSpawns;
    }

    void OnSpawnCountChanged(int oldValue, int newValue)
    {
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (stationRenderer != null)
        {
            stationRenderer.material.color = CanSpawn() ? availableColor : unavailableColor;
        }
    }
}