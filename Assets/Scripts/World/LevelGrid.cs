using System.Collections.Generic;
using UnityEngine;
using System;

public class LevelGrid : MonoBehaviour
{
    public static LevelGrid Instance { get; private set; }
    
    [Header("Settings")]
    [SerializeField] private float tileSize = 2f;
    [SerializeField] private Vector2Int gridSize = new Vector2Int(20, 20);
    [SerializeField] private bool debugDraw = false;
    
    private Dictionary<Vector2Int, PlaceableObject> gridObjects = new Dictionary<Vector2Int, PlaceableObject>();
    
    // Events
    public event Action<Vector2Int, PlaceableObject> OnObjectPlaced;
    public event Action<Vector2Int, PlaceableObject> OnObjectRemoved;
    
    public float TileSize => tileSize;
    public Vector2Int GridSize => gridSize;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
        return IsWithinBounds(gridPos) && !gridObjects.ContainsKey(gridPos);
    }
    
    public void RegisterObject(Vector2Int gridPos, PlaceableObject obj)
    {
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
        OnObjectPlaced?.Invoke(gridPos, obj);
    }
    
    public void UnregisterObject(Vector2Int gridPos)
    {
        if (gridObjects.TryGetValue(gridPos, out PlaceableObject obj))
        {
            gridObjects.Remove(gridPos);
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