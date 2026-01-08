using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Invisible component placed in level editor that defines what should be built at this cell
/// Tracks recipe progress and validates material placement
/// NOTE: This is a MonoBehaviour (not NetworkBehaviour) since it's a scene object
/// </summary>
public class CellBlueprint : MonoBehaviour
{
    [Header("Blueprint Configuration")]
    [SerializeField] private Recipe recipe;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private bool autoCalculateGridPosition = true;
    [SerializeField] private GamePhase requiredPhase = GamePhase.Foundation;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject blueprintVisual;
    
    // Track progress per material type (simple ints, synced via server processing)
    private int concreteProcessed = 0;
    private int steelProcessed = 0;
    private int woodProcessed = 0;
    private int brickProcessed = 0;
    private int glassProcessed = 0;
    
    private bool isComplete = false;
    private bool isDamaged = false;
    
    public Recipe Recipe => recipe;
    public Vector2Int GridPosition => gridPosition;
    public bool IsComplete => isComplete;
    public GamePhase RequiredPhase => requiredPhase;

    void OnEnable()
    {
        // Try to register as early as possible
        if (LevelGrid.Instance != null && recipe != null)
        {
            if (autoCalculateGridPosition)
            {
                gridPosition = LevelGrid.Instance.WorldToGrid(transform.position);
            }
            LevelGrid.Instance.RegisterBlueprint(gridPosition, recipe.TargetLayer, this);
            Debug.Log($"[Blueprint OnEnable] Registered at {gridPosition} on {recipe.TargetLayer} layer");
        }
    }

    void Start()
    {
        Debug.Log($"=== CELLBLUEPRINT START DEBUG ===");
        Debug.Log($"GameObject name: {gameObject.name}");
        Debug.Log($"Transform position: {transform.position}");
        Debug.Log($"Recipe field: {recipe}");
        Debug.Log($"Recipe is null: {recipe == null}");
        Debug.Log($"Required Phase: {requiredPhase}");
        
        if (recipe != null)
        {
            Debug.Log($"Recipe name: {recipe.RecipeName}");
            Debug.Log($"Recipe target layer: {recipe.TargetLayer}");
            Debug.Log($"Required materials count: {recipe.RequiredMaterials?.Count ?? 0}");
        }
        else
        {
            Debug.LogError($"!!! RECIPE IS NULL ON {gameObject.name} !!!");
        }
        
        if (autoCalculateGridPosition && LevelGrid.Instance != null)
        {
            gridPosition = LevelGrid.Instance.WorldToGrid(transform.position);
            Debug.Log($"Auto-calculated grid position: {gridPosition}");
        }
        else
        {
            Debug.Log($"Using manual grid position: {gridPosition}");
        }
        
        // Register with grid
        if (LevelGrid.Instance != null && recipe != null)
        {
            LevelGrid.Instance.RegisterBlueprint(gridPosition, recipe.TargetLayer, this);
            
            // Verify it registered
            var found = LevelGrid.Instance.GetBlueprint(gridPosition, recipe.TargetLayer);
            Debug.Log($"Registered and verified: {found != null}");
            if (found != null)
            {
                Debug.Log($"Found recipe after registration: {found.Recipe}");
            }
        }
        else
        {
            Debug.LogWarning("LevelGrid.Instance or recipe is null in Start");
        }
        
        CreateBlueprintVisual();
        UpdateBlueprintVisibility();
        Debug.Log($"=== END CELLBLUEPRINT START DEBUG ===");
    }
    
    void Update()
    {
        // Update visibility based on phase
        UpdateBlueprintVisibility();
    }
    
    /// <summary>
    /// Check if this blueprint is active in the current game phase
    /// </summary>
    public bool IsActiveInCurrentPhase()
    {
        if (recipe == null) return false;
        if (GameStateManager.Instance == null) return false;
        
        GamePhase currentPhase = GameStateManager.Instance.GetCurrentPhase();
        
        // Can build in your phase if not complete
        if (currentPhase == requiredPhase && !isComplete) return true;
        
        // Can repair in later phases if damaged
        if ((int)currentPhase > (int)requiredPhase && isDamaged) return true;
        
        return false;
    }
    
    void UpdateBlueprintVisibility()
    {
        if (blueprintVisual == null) return;
        
        // Show blueprint only if active in current phase
        bool shouldBeVisible = IsActiveInCurrentPhase();
        
        if (blueprintVisual.activeSelf != shouldBeVisible)
        {
            blueprintVisual.SetActive(shouldBeVisible);
        }
    }

    void CreateBlueprintVisual()
    {
        if (recipe == null || recipe.ResultPrefab == null) return;
        
        // Create ghost visual
        blueprintVisual = Instantiate(recipe.ResultPrefab, transform);
        blueprintVisual.name = "Blueprint Visual";
        
        // Position at grid location
        if (LevelGrid.Instance != null)
        {
            Vector3 worldPos = LevelGrid.Instance.GridToWorld(gridPosition, 0);
            blueprintVisual.transform.position = worldPos;
        }
        
        // Make it ghostly
        Renderer[] renderers = blueprintVisual.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (recipe.BlueprintMaterial != null)
            {
                rend.material = recipe.BlueprintMaterial;
            }
            else
            {
                // Fallback: make semi-transparent
                Material mat = new Material(rend.material);
                Color color = mat.color;
                color.a = 0.3f;
                mat.color = color;
                rend.material = mat;
            }
        }
        
        // Remove colliders and physics
        Collider[] colliders = blueprintVisual.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            Destroy(col);
        }
        
        Rigidbody[] rigidbodies = blueprintVisual.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            Destroy(rb);
        }
    }

    void UpdateBlueprintVisual()
    {
        // Could fade in blueprint as materials are processed
        if (blueprintVisual != null)
        {
            float progress = GetCompletionPercentage();
            
            Renderer[] renderers = blueprintVisual.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                Color color = rend.material.color;
                color.a = 0.3f + (progress * 0.4f); // 0.3 to 0.7 alpha
                rend.material.color = color;
            }
        }
    }

    /// <summary>
    /// Check if this tool type is needed for this blueprint
    /// </summary>
    public bool AcceptsTool(string toolType)
    {
        if (recipe == null) return false;
        return recipe.RequiresTool(toolType);
    }

    /// <summary>
    /// Process one material with the given tool
    /// Returns true if processing succeeded
    /// </summary>
    public bool ProcessMaterial(string toolType, MaterialType materialType)
    {
        if (recipe == null) return false;
        if (isComplete) return false;
        
        // Check if this tool matches a material in the recipe
        MaterialType? expectedMaterial = recipe.GetMaterialForTool(toolType);
        if (expectedMaterial == null || expectedMaterial != materialType)
        {
            Debug.Log($"[Blueprint] Tool {toolType} doesn't match material {materialType}");
            return false;
        }
        
        // Check if we need more of this material
        int required = recipe.GetRequiredCount(materialType);
        int current = GetProcessedCount(materialType);
        
        if (current >= required)
        {
            Debug.Log($"[Blueprint] Already have enough {materialType} ({current}/{required})");
            return false;
        }
        
        // Process material
        IncrementProcessedCount(materialType);
        Debug.Log($"[Blueprint] Processed {materialType}: {current + 1}/{required}");
        
        return true;
    }

    int GetProcessedCount(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Concrete: return concreteProcessed;
            case MaterialType.Steel: return steelProcessed;
            case MaterialType.Wood: return woodProcessed;
            case MaterialType.Brick: return brickProcessed;
            case MaterialType.Glass: return glassProcessed;
            default: return 0;
        }
    }

    void IncrementProcessedCount(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Concrete: concreteProcessed++; break;
            case MaterialType.Steel: steelProcessed++; break;
            case MaterialType.Wood: woodProcessed++; break;
            case MaterialType.Brick: brickProcessed++; break;
            case MaterialType.Glass: glassProcessed++; break;
        }
        
        // Update visuals and check completion after incrementing
        UpdateBlueprintVisual();
        CheckCompletion();
    }

    void CheckCompletion()
    {
        if (recipe == null) return;
        if (isComplete) return;
        
        // Check if all materials are processed
        foreach (var requirement in recipe.RequiredMaterials)
        {
            int processed = GetProcessedCount(requirement.materialType);
            if (processed < requirement.count)
            {
                return; // Not complete yet
            }
        }
        
        // Recipe complete!
        CompleteRecipe();
    }

    void CompleteRecipe()
    {
        isComplete = true;
        Debug.Log($"[Blueprint] Recipe complete at {gridPosition}! Spawning {recipe.RecipeName}");
        
        // Only spawn on server
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            // Spawn the final object
            if (recipe.ResultPrefab != null && LevelGrid.Instance != null)
            {
                Vector3 spawnPos = LevelGrid.Instance.GridToWorld(gridPosition, 0);
                GameObject resultObj = Instantiate(recipe.ResultPrefab, spawnPos, Quaternion.identity);
                
                Unity.Netcode.NetworkObject netObj = resultObj.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                    
                    // If it's a placeable object, place it automatically
                    PlaceableObject placeable = resultObj.GetComponent<PlaceableObject>();
                    if (placeable != null)
                    {
                        placeable.OnPlaced(gridPosition);
                        // Recipe is complete, so object should be refined immediately
                        placeable.Refine();
                        Debug.Log($"[SERVER] Set spawned object to Refined state at {gridPosition}");
                    }
                }
            }
        }
        
        // Hide blueprint visual (on all clients)
        if (blueprintVisual != null)
        {
            blueprintVisual.SetActive(false);
        }
    }

    public float GetCompletionPercentage()
    {
        if (recipe == null) return 0f;
        
        int totalRequired = recipe.GetTotalMaterialCount();
        int totalProcessed = 0;
        
        foreach (var requirement in recipe.RequiredMaterials)
        {
            totalProcessed += GetProcessedCount(requirement.materialType);
        }
        
        return totalRequired > 0 ? (float)totalProcessed / totalRequired : 0f;
    }

    public Dictionary<MaterialType, int> GetProgress()
    {
        Dictionary<MaterialType, int> progress = new Dictionary<MaterialType, int>();
        
        if (recipe != null)
        {
            foreach (var requirement in recipe.RequiredMaterials)
            {
                progress[requirement.materialType] = GetProcessedCount(requirement.materialType);
            }
        }
        
        return progress;
    }

    void OnDrawGizmos()
    {
        if (recipe == null) return;
        
        Gizmos.color = isComplete ? Color.green : Color.yellow;
        
        Vector3 worldPos;
        if (LevelGrid.Instance != null)
        {
            worldPos = LevelGrid.Instance.GridToWorld(gridPosition, 0);
        }
        else
        {
            worldPos = transform.position;
        }
        
        Gizmos.DrawWireCube(worldPos, Vector3.one * 1.8f);
        
        // Draw progress text in scene view
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(worldPos + Vector3.up * 2, 
            $"{recipe.RecipeName}\n{GetCompletionPercentage() * 100:F0}%");
        #endif
    }
}