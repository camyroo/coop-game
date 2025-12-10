using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class ObjectSpawner : NetworkBehaviour
{
    public static ObjectSpawner Instance { get; private set; }
    
    [Header("Spawn Settings")]
    [SerializeField] private GameObject[] spawnablePrefabs;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool spawnOnGameStart = true;
    
    [Header("Spawn Limits")]
    [SerializeField] private int maxObjectsPerType = 10;
    [SerializeField] private float spawnInterval = 2f;
    
    private Dictionary<GameObject, List<GameObject>> spawnedObjects = new Dictionary<GameObject, List<GameObject>>();
    private float lastSpawnTime;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    void Start()
    {
        if (spawnOnGameStart && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStart += OnGameStart;
        }
    }
    
    void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStart -= OnGameStart;
        }
    }
    
    void OnGameStart()
    {
        if (IsServer)
        {
            SpawnInitialObjects();
        }
    }
    
    #region Spawning
    
    void SpawnInitialObjects()
    {
        foreach (GameObject prefab in spawnablePrefabs)
        {
            for (int i = 0; i < maxObjectsPerType && i < spawnPoints.Length; i++)
            {
                SpawnObject(prefab, spawnPoints[i].position, Quaternion.identity);
            }
        }
    }
    
    public GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!IsServer)
        {
            Debug.LogWarning("Only server can spawn objects!");
            return null;
        }
        
        // Check spawn limit
        if (!CanSpawn(prefab))
        {
            Debug.LogWarning($"Cannot spawn {prefab.name}, limit reached!");
            return null;
        }
        
        // Instantiate and spawn
        GameObject obj = Instantiate(prefab, position, rotation);
        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        
        if (netObj != null)
        {
            netObj.Spawn();
            TrackSpawnedObject(prefab, obj);
            return obj;
        }
        
        Debug.LogError($"Prefab {prefab.name} doesn't have NetworkObject component!");
        Destroy(obj);
        return null;
    }
    
    public void DespawnObject(GameObject obj)
    {
        if (!IsServer) return;
        
        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Despawn();
            UntrackSpawnedObject(obj);
        }
    }
    
    #endregion
    
    #region Tracking
    
    void TrackSpawnedObject(GameObject prefab, GameObject instance)
    {
        if (!spawnedObjects.ContainsKey(prefab))
        {
            spawnedObjects[prefab] = new List<GameObject>();
        }
        spawnedObjects[prefab].Add(instance);
    }
    
    void UntrackSpawnedObject(GameObject instance)
    {
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Value.Contains(instance))
            {
                kvp.Value.Remove(instance);
                return;
            }
        }
    }
    
    bool CanSpawn(GameObject prefab)
    {
        if (!spawnedObjects.ContainsKey(prefab))
        {
            return true;
        }
        
        // Remove null references
        spawnedObjects[prefab].RemoveAll(obj => obj == null);
        
        return spawnedObjects[prefab].Count < maxObjectsPerType;
    }
    
    public int GetSpawnedCount(GameObject prefab)
    {
        if (!spawnedObjects.ContainsKey(prefab))
        {
            return 0;
        }
        
        spawnedObjects[prefab].RemoveAll(obj => obj == null);
        return spawnedObjects[prefab].Count;
    }
    
    #endregion
    
    #region RPC Methods
    
    [ServerRpc(RequireOwnership = false)]
    public void SpawnObjectServerRpc(int prefabIndex, Vector3 position, Quaternion rotation)
    {
        if (prefabIndex >= 0 && prefabIndex < spawnablePrefabs.Length)
        {
            SpawnObject(spawnablePrefabs[prefabIndex], position, rotation);
        }
    }
    
    #endregion
}