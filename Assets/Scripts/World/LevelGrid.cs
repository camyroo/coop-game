using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Netcode;

public class LevelGrid : NetworkBehaviour
{
    public static LevelGrid Instance { get; private set; }

    [Header("Grid Settings")]
    [SerializeField] private float cellSize = 2f; 
    [SerializeField] private Vector2Int gridBounds = new Vector2Int(20, 20);

    [Header("Visualization")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private Material gridMaterial;

    [Header("Grid Highlight")]
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private Color highlightValidColor = new Color(0, 1, 0, 0.3f);
    [SerializeField] private Color highlightInvalidColor = new Color(1, 0, 0, 0.3f);
    [SerializeField] private Color highlightToolColor = new Color(1, 0.5f, 0, 0.3f);

    private Dictionary<Vector2Int, Dictionary<GridLayer, PlaceableObject>> gridCells = new Dictionary<Vector2Int, Dictionary<GridLayer, PlaceableObject>>();
    private GameObject gridHighlight;

    public float CellSize => cellSize;

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
        if (showGrid) CreateGridVisual();
        CreateHighlight();
    }

    #region Grid Conversion

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / cellSize),
            Mathf.FloorToInt(worldPos.z / cellSize)
        );
    }

    public Vector3 GridToWorld(Vector2Int gridPos, float yHeight = 0)
    {
        return new Vector3(
            (gridPos.x + 0.5f) * cellSize, 
            yHeight, 
            (gridPos.y + 0.5f) * cellSize
        );
    }

    public bool IsInBounds(Vector2Int gridPos)
    {
        int halfX = gridBounds.x / 2;
        int halfY = gridBounds.y / 2;
        
        return gridPos.x >= -halfX && gridPos.x < halfX &&
            gridPos.y >= -halfY && gridPos.y < halfY;
    }

    #endregion

    #region Placement Management

    public bool CanPlaceAt(Vector2Int gridPos, GridLayer layer)
    {
        if (!IsInBounds(gridPos)) return false;
        
        // Check if this specific layer is occupied
        if (gridCells.TryGetValue(gridPos, out Dictionary<GridLayer, PlaceableObject> cellLayers))
        {
            return !cellLayers.ContainsKey(layer);
        }
        
        return true; // Cell doesn't exist yet, so it's free
    }

    public void Register(Vector2Int gridPos, GridLayer layer, PlaceableObject obj)
    {
        // Server validates placement, clients just track it
        if (IsServer && !CanPlaceAt(gridPos, layer))
        {
            Debug.LogWarning($"Cannot register at {gridPos} on layer {layer}, already occupied");
            return;
        }

        // Create cell dictionary if it doesn't exist
        if (!gridCells.ContainsKey(gridPos))
        {
            gridCells[gridPos] = new Dictionary<GridLayer, PlaceableObject>();
        }

        // Register object on specific layer
        gridCells[gridPos][layer] = obj;
        Debug.Log($"[{(IsServer ? "SERVER" : "CLIENT")}] Registered {layer} at {gridPos}");
    }

    public void Unregister(Vector2Int gridPos, GridLayer layer)
    {
        if (gridCells.TryGetValue(gridPos, out Dictionary<GridLayer, PlaceableObject> cellLayers))
        {
            cellLayers.Remove(layer);
            
            // Clean up empty cell dictionaries
            if (cellLayers.Count == 0)
            {
                gridCells.Remove(gridPos);
            }
            
            Debug.Log($"[{(IsServer ? "SERVER" : "CLIENT")}] Unregistered {layer} at {gridPos}");
        }
    }

    public PlaceableObject GetObjectAt(Vector2Int gridPos, GridLayer layer)
    {
        if (gridCells.TryGetValue(gridPos, out Dictionary<GridLayer, PlaceableObject> cellLayers))
        {
            cellLayers.TryGetValue(layer, out PlaceableObject obj);
            return obj;
        }
        return null;
    }

    public bool HasFoundation(Vector2Int gridPos)
    {
        return GetObjectAt(gridPos, GridLayer.Foundation) != null;
    }

    public List<PlaceableObject> GetAllObjectsAt(Vector2Int gridPos)
    {
        List<PlaceableObject> objects = new List<PlaceableObject>();
        
        if (gridCells.TryGetValue(gridPos, out Dictionary<GridLayer, PlaceableObject> cellLayers))
        {
            objects.AddRange(cellLayers.Values);
        }
        
        return objects;
    }

    #endregion

    #region Visualization

    void CreateGridVisual()
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Grid Plane";
        Destroy(plane.GetComponent<Collider>());

        float sizeX = gridBounds.x * cellSize;
        float sizeZ = gridBounds.y * cellSize;
        plane.transform.localScale = new Vector3(sizeX / 10f, 1f, sizeZ / 10f);
        plane.transform.position = new Vector3(0, 0.01f, 0);

        if (gridMaterial != null)
        {
            plane.GetComponent<Renderer>().material = gridMaterial;
            gridMaterial.SetFloat("_CellSize", cellSize);
        }
    }

    void CreateHighlight()
    {
        gridHighlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gridHighlight.name = "Grid Highlight";
        Destroy(gridHighlight.GetComponent<Collider>());

        gridHighlight.transform.localScale = new Vector3(cellSize * 0.95f, 0.1f, cellSize * 0.95f);
        gridHighlight.transform.position = new Vector3(0, 0.02f, 0);

        if (highlightMaterial != null)
        {
            // Create an instance of the material for this highlight
            Material matInstance = new Material(highlightMaterial);
            gridHighlight.GetComponent<Renderer>().material = matInstance;
        }

        gridHighlight.SetActive(false);
    }

    public void ShowHighlight(Vector2Int gridPos, bool isValid)
    {
        if (gridHighlight == null) return;
        if (!IsInBounds(gridPos)) return;

        gridHighlight.SetActive(true);
        gridHighlight.transform.position = GridToWorld(gridPos, 0.02f);

        Renderer rend = gridHighlight.GetComponent<Renderer>();
        if (rend != null)
        {
            Color color = isValid ? highlightValidColor : highlightInvalidColor;
            rend.material.color = color;
        }
    }

    public void ShowToolHighlight(Vector2Int gridPos, bool canUse)
    {
        if (gridHighlight == null) return;
        if (!IsInBounds(gridPos)) return;

        gridHighlight.SetActive(true);
        gridHighlight.transform.position = GridToWorld(gridPos, 0.02f);

        Renderer rend = gridHighlight.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = highlightToolColor;
        }
    }

    public void HideHighlight()
    {
        if (gridHighlight != null)
        {
            gridHighlight.SetActive(false);
        }
    }

    #endregion

    #region Debug

    void OnDrawGizmos()
    {
        if (!showGrid) return;

        Gizmos.color = Color.green;
        for (int x = -gridBounds.x / 2; x < gridBounds.x / 2; x++)
        {
            for (int z = -gridBounds.y / 2; z < gridBounds.y / 2; z++)
            {
                Vector2Int gridPos = new Vector2Int(x, z);
                Vector3 worldPos = GridToWorld(gridPos);

                Gizmos.DrawWireCube(worldPos, new Vector3(cellSize * 0.9f, 0.1f, cellSize * 0.9f));

                // Check if any layer is occupied at this position
                if (Application.isPlaying && gridCells.ContainsKey(gridPos))
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(worldPos, new Vector3(cellSize * 0.8f, 0.2f, cellSize * 0.8f));
                    Gizmos.color = Color.green;
                }
            }
        }
    }

    #endregion
}