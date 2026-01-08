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

    // Track refined objects (one per layer per cell)
    private Dictionary<Vector2Int, Dictionary<GridLayer, PlaceableObject>> gridCells = new Dictionary<Vector2Int, Dictionary<GridLayer, PlaceableObject>>();
    
    // Track raw materials (multiple per layer per cell - they stack)
    private Dictionary<Vector2Int, Dictionary<GridLayer, List<RawMaterial>>> rawMaterialCells = new Dictionary<Vector2Int, Dictionary<GridLayer, List<RawMaterial>>>();
    
    // Track blueprints (one per layer per cell - foundation + wall + decor can coexist)
    private Dictionary<Vector2Int, Dictionary<GridLayer, CellBlueprint>> cellBlueprints = new Dictionary<Vector2Int, Dictionary<GridLayer, CellBlueprint>>();
    
    private GameObject gridHighlight;

    public float CellSize => cellSize;

    void Awake()
    {
        Debug.Log("[LevelGrid] Awake called!");
        
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LevelGrid] Duplicate found, destroying!");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        Debug.Log("[LevelGrid] Instance set successfully!");
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

    #region Blueprint Management

    public void RegisterBlueprint(Vector2Int gridPos, GridLayer layer, CellBlueprint blueprint)
    {
        if (!cellBlueprints.ContainsKey(gridPos))
        {
            cellBlueprints[gridPos] = new Dictionary<GridLayer, CellBlueprint>();
        }
        
        cellBlueprints[gridPos][layer] = blueprint;
        Debug.Log($"[LevelGrid] Registered blueprint at {gridPos} on {layer} layer: {blueprint.Recipe?.RecipeName ?? "NULL"}");
    }

    public CellBlueprint GetBlueprint(Vector2Int gridPos, GridLayer layer)
    {
        if (cellBlueprints.TryGetValue(gridPos, out var layerDict))
        {
            layerDict.TryGetValue(layer, out CellBlueprint blueprint);
            return blueprint;
        }
        return null;
    }
    
    public CellBlueprint GetActiveBlueprint(Vector2Int gridPos)
    {
        if (!cellBlueprints.ContainsKey(gridPos)) return null;
        
        GamePhase currentPhase = GameStateManager.Instance?.GetCurrentPhase() ?? GamePhase.Foundation;
        
        // Check each layer for active blueprint matching current phase
        foreach (var kvp in cellBlueprints[gridPos])
        {
            CellBlueprint blueprint = kvp.Value;
            if (blueprint.IsActiveInCurrentPhase())
            {
                return blueprint;
            }
        }
        
        return null;
    }

    public bool HasBlueprint(Vector2Int gridPos, GridLayer layer)
    {
        if (cellBlueprints.TryGetValue(gridPos, out var layerDict))
        {
            return layerDict.ContainsKey(layer);
        }
        return false;
    }
    
    public List<CellBlueprint> GetAllBlueprintsAt(Vector2Int gridPos)
    {
        List<CellBlueprint> blueprints = new List<CellBlueprint>();
        
        if (cellBlueprints.TryGetValue(gridPos, out var layerDict))
        {
            blueprints.AddRange(layerDict.Values);
        }
        
        return blueprints;
    }

    #endregion

    #region Raw Material Management

    public void RegisterRawMaterial(Vector2Int gridPos, GridLayer layer, RawMaterial material)
    {
        if (!rawMaterialCells.ContainsKey(gridPos))
        {
            rawMaterialCells[gridPos] = new Dictionary<GridLayer, List<RawMaterial>>();
        }

        if (!rawMaterialCells[gridPos].ContainsKey(layer))
        {
            rawMaterialCells[gridPos][layer] = new List<RawMaterial>();
        }

        rawMaterialCells[gridPos][layer].Add(material);
        Debug.Log($"[{(IsServer ? "SERVER" : "CLIENT")}] Registered raw {material.MaterialType} at {gridPos} on {layer}, count: {rawMaterialCells[gridPos][layer].Count}");
    }

    public void UnregisterRawMaterial(Vector2Int gridPos, GridLayer layer, RawMaterial material)
    {
        if (rawMaterialCells.TryGetValue(gridPos, out var layerDict))
        {
            if (layerDict.TryGetValue(layer, out var materials))
            {
                materials.Remove(material);
                
                if (materials.Count == 0)
                {
                    layerDict.Remove(layer);
                }
                
                if (layerDict.Count == 0)
                {
                    rawMaterialCells.Remove(gridPos);
                }
                
                Debug.Log($"[{(IsServer ? "SERVER" : "CLIENT")}] Unregistered raw material at {gridPos}");
            }
        }
    }

    public List<RawMaterial> GetRawMaterials(Vector2Int gridPos, GridLayer layer)
    {
        if (rawMaterialCells.TryGetValue(gridPos, out var layerDict))
        {
            if (layerDict.TryGetValue(layer, out var materials))
            {
                return new List<RawMaterial>(materials);
            }
        }
        return new List<RawMaterial>();
    }

    public int GetRawMaterialCount(Vector2Int gridPos, GridLayer layer)
    {
        return GetRawMaterials(gridPos, layer).Count;
    }

    public bool HasRawMaterials(Vector2Int gridPos, GridLayer layer)
    {
        return GetRawMaterialCount(gridPos, layer) > 0;
    }

    #endregion

    #region Refined Object Management

    public bool CanPlaceAt(Vector2Int gridPos, GridLayer layer)
    {
        if (!IsInBounds(gridPos)) return false;
        
        if (gridCells.TryGetValue(gridPos, out Dictionary<GridLayer, PlaceableObject> cellLayers))
        {
            return !cellLayers.ContainsKey(layer);
        }
        
        return true;
    }

    public void Register(Vector2Int gridPos, GridLayer layer, PlaceableObject obj)
    {
        if (IsServer && !CanPlaceAt(gridPos, layer))
        {
            Debug.LogWarning($"Cannot register at {gridPos} on layer {layer}, already occupied");
            return;
        }

        if (!gridCells.ContainsKey(gridPos))
        {
            gridCells[gridPos] = new Dictionary<GridLayer, PlaceableObject>();
        }

        gridCells[gridPos][layer] = obj;
        Debug.Log($"[{(IsServer ? "SERVER" : "CLIENT")}] Registered refined {layer} at {gridPos}");
    }

    public void Unregister(Vector2Int gridPos, GridLayer layer)
    {
        if (gridCells.TryGetValue(gridPos, out Dictionary<GridLayer, PlaceableObject> cellLayers))
        {
            cellLayers.Remove(layer);
            
            if (cellLayers.Count == 0)
            {
                gridCells.Remove(gridPos);
            }
            
            Debug.Log($"[{(IsServer ? "SERVER" : "CLIENT")}] Unregistered refined {layer} at {gridPos}");
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

                if (!Application.isPlaying) continue;
                
                // Show if cell has refined objects
                if (gridCells.ContainsKey(gridPos))
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawCube(worldPos, new Vector3(cellSize * 0.8f, 0.2f, cellSize * 0.8f));
                    Gizmos.color = Color.green;
                }
                
                // Show if cell has raw materials
                if (rawMaterialCells.ContainsKey(gridPos))
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(worldPos + Vector3.up * 0.5f, new Vector3(cellSize * 0.7f, 0.3f, cellSize * 0.7f));
                    Gizmos.color = Color.green;
                }
                
                // Show if cell has blueprints (multiple possible)
                if (cellBlueprints.ContainsKey(gridPos))
                {
                    int blueprintCount = cellBlueprints[gridPos].Count;
                    Gizmos.color = Color.cyan;
                    
                    // Draw multiple boxes for multiple blueprints
                    for (int i = 0; i < blueprintCount; i++)
                    {
                        float yOffset = i * 0.2f;
                        Gizmos.DrawWireCube(worldPos + Vector3.up * yOffset, new Vector3(cellSize * 0.6f, 0.15f, cellSize * 0.6f));
                    }
                    
                    Gizmos.color = Color.green;
                }
            }
        }
    }

    #endregion
}