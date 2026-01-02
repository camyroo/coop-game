using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Netcode;

public class LevelGrid : NetworkBehaviour
{
    public static LevelGrid Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private GameConfig config;

    [Header("Settings")]
    [SerializeField] private bool debugDraw = false;

    [Header("Visual Grid")]
    [SerializeField] private Material gridMaterial;
    [SerializeField] private bool showGridInGame = true;

    private float tileSize => config != null ? config.gridTileSize : 2f;
    private Vector2Int gridSize => config != null ? config.gridSize : new Vector2Int(20, 20);

    private Dictionary<Vector2Int, PlaceableObject> gridObjects = new Dictionary<Vector2Int, PlaceableObject>();
    private NetworkList<Vector2Int> occupiedPositions;

    // Events
    public event Action<Vector2Int, PlaceableObject> OnObjectPlaced;
    public event Action<Vector2Int, PlaceableObject> OnObjectRemoved;

    public float TileSize => tileSize;
    public Vector2Int GridSize => gridSize;



    void Start()
    {
        if (showGridInGame)
        {
            CreateGridPlane();
        }
    }


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        occupiedPositions = new NetworkList<Vector2Int>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            // Client: populate local grid from network list
            occupiedPositions.OnListChanged += OnOccupiedPositionsChanged;
            SyncGridFromNetwork();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (!IsServer)
        {
            occupiedPositions.OnListChanged -= OnOccupiedPositionsChanged;
        }
    }

    void OnOccupiedPositionsChanged(NetworkListEvent<Vector2Int> changeEvent)
    {
        // Rebuild grid state from network list on clients
        SyncGridFromNetwork();
    }

    void SyncGridFromNetwork()
    {
        // Clear client-side tracking (keep objects dictionary for later use)
        foreach (var pos in new List<Vector2Int>(gridObjects.Keys))
        {
            if (!occupiedPositions.Contains(pos))
            {
                gridObjects.Remove(pos);
            }
        }
    }

    void CreateGridPlane()
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Grid Plane";
        Destroy(plane.GetComponent<Collider>()); // Remove collider so it doesn't interfere

        // Calculate plane size based on grid dimensions
        float sizeX = gridSize.x * tileSize;
        float sizeZ = gridSize.y * tileSize;

        // Unity planes are 10x10 by default, so scale accordingly
        plane.transform.localScale = new Vector3(sizeX / 10f, 1f, sizeZ / 10f);
        plane.transform.position = new Vector3(0, 0.01f, 0); // Slightly above ground

        // Apply the grid material
        plane.GetComponent<Renderer>().material = gridMaterial;

        // Update shader properties to match your grid
        if (gridMaterial != null)
        {
            gridMaterial.SetFloat("_CellSize", tileSize);
        }
    }


    #region Grid Conversion

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x / tileSize);
        int z = Mathf.RoundToInt(worldPos.z / tileSize);
        return new Vector2Int(x, z);
    }

    public Vector3 GridToWorld(Vector2Int gridPos, float yHeight = 0)
    {
        return new Vector3(gridPos.x * tileSize, yHeight, gridPos.y * tileSize);
    }

    public bool IsWithinBounds(Vector2Int gridPos)
    {
        return gridPos.x >= -gridSize.x / 2 && gridPos.x < gridSize.x / 2 &&
               gridPos.y >= -gridSize.y / 2 && gridPos.y < gridSize.y / 2;
    }

    #endregion

    #region Grid Management

    public bool CanPlaceAt(Vector2Int gridPos)
    {
        if (!IsWithinBounds(gridPos)) return false;

        // Check both local dictionary and network list
        if (IsServer)
        {
            return !gridObjects.ContainsKey(gridPos);
        }
        else
        {
            return !occupiedPositions.Contains(gridPos);
        }
    }

    public void RegisterObject(Vector2Int gridPos, PlaceableObject obj)
    {
        if (!IsServer)
        {
            Debug.LogWarning("Only server can register objects!");
            return;
        }

        if (!IsWithinBounds(gridPos))
        {
            Debug.LogWarning($"Trying to place object outside grid bounds: {gridPos}");
            return;
        }

        if (gridObjects.ContainsKey(gridPos))
        {
            Debug.LogWarning($"Grid position {gridPos} already occupied!");
            return;
        }

        gridObjects[gridPos] = obj;
        occupiedPositions.Add(gridPos);
        OnObjectPlaced?.Invoke(gridPos, obj);
    }

    public void UnregisterObject(Vector2Int gridPos)
    {
        if (!IsServer)
        {
            Debug.LogWarning("Only server can unregister objects!");
            return;
        }

        if (gridObjects.TryGetValue(gridPos, out PlaceableObject obj))
        {
            gridObjects.Remove(gridPos);
            occupiedPositions.Remove(gridPos);
            OnObjectRemoved?.Invoke(gridPos, obj);
        }
    }

    public PlaceableObject GetObjectAt(Vector2Int gridPos)
    {
        gridObjects.TryGetValue(gridPos, out PlaceableObject obj);
        return obj;
    }

    public void ClearGrid()
    {
        gridObjects.Clear();
    }

    public int GetObjectCount()
    {
        return gridObjects.Count;
    }

    #endregion

    #region Debug Visualization

    void OnDrawGizmos()
    {
        if (!debugDraw) return;

        Gizmos.color = Color.green;

        for (int x = -gridSize.x / 2; x < gridSize.x / 2; x++)
        {
            for (int z = -gridSize.y / 2; z < gridSize.y / 2; z++)
            {
                Vector2Int gridPos = new Vector2Int(x, z);
                Vector3 worldPos = GridToWorld(gridPos);

                // Draw grid square
                Gizmos.DrawWireCube(worldPos, new Vector3(tileSize * 0.9f, 0.1f, tileSize * 0.9f));

                // Highlight occupied cells
                if (Application.isPlaying && gridObjects.ContainsKey(gridPos))
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(worldPos, new Vector3(tileSize * 0.8f, 0.2f, tileSize * 0.8f));
                    Gizmos.color = Color.green;
                }
            }
        }
    }

    #endregion
}