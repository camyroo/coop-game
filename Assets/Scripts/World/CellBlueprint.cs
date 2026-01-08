using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// CellBlueprint - Simplified version where blueprint visual BECOMES the final object
/// No spawning duplicates!
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
    
    // Track progress per material type
    private int concreteProcessed = 0;
    private int steelProcessed = 0;
    private int woodProcessed = 0;
    private int brickProcessed = 0;
    private int glassProcessed = 0;
    
    private bool isComplete = false;
    private bool isDamaged = false;
    
    // Store original materials for restoration
    private Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();
    
    public Recipe Recipe => recipe;
    public Vector2Int GridPosition => gridPosition;
    public bool IsComplete => isComplete;
    public GamePhase RequiredPhase => requiredPhase;

    void OnEnable()
    {
        if (LevelGrid.Instance != null && recipe != null)
        {
            if (autoCalculateGridPosition)
            {
                gridPosition = LevelGrid.Instance.WorldToGrid(transform.position);
            }
            LevelGrid.Instance.RegisterBlueprint(gridPosition, recipe.TargetLayer, this);
        }
    }

    void Start()
    {
        if (autoCalculateGridPosition && LevelGrid.Instance != null)
        {
            gridPosition = LevelGrid.Instance.WorldToGrid(transform.position);
        }
        
        if (LevelGrid.Instance != null && recipe != null)
        {
            LevelGrid.Instance.RegisterBlueprint(gridPosition, recipe.TargetLayer, this);
        }
        
        CreateBlueprintVisual();
        UpdateBlueprintVisibility();
    }
    
    void Update()
    {
        UpdateBlueprintVisibility();
    }
    
    public bool IsActiveInCurrentPhase()
    {
        if (recipe == null) return false;
        if (GameStateManager.Instance == null) return false;
        
        GamePhase currentPhase = GameStateManager.Instance.GetCurrentPhase();
        
        if (currentPhase == requiredPhase && !isComplete) return true;
        if ((int)currentPhase > (int)requiredPhase && isDamaged) return true;
        
        return false;
    }
    
    void UpdateBlueprintVisibility()
    {
        if (blueprintVisual == null) return;
        
        // Always show if complete (real object now)
        if (isComplete)
        {
            if (!blueprintVisual.activeSelf)
            {
                Debug.Log($"[Blueprint] Showing completed {recipe.RecipeName} at {gridPosition} (Phase: {GameStateManager.Instance?.GetCurrentPhase()})");
            }
            blueprintVisual.SetActive(true);
            return;
        }
        
        // For incomplete blueprints, only show if active in current phase
        bool shouldBeVisible = IsActiveInCurrentPhase();
        
        if (blueprintVisual.activeSelf != shouldBeVisible)
        {
            Debug.Log($"[Blueprint] {recipe.RecipeName} at {gridPosition} visibility: {shouldBeVisible} (Complete: {isComplete}, Phase: {GameStateManager.Instance?.GetCurrentPhase()}, Required: {requiredPhase})");
            blueprintVisual.SetActive(shouldBeVisible);
        }
    }

    void CreateBlueprintVisual()
    {
        if (recipe == null || recipe.ResultPrefab == null) return;
        
        // Calculate position with offset
        Vector3 baseWorldPos = LevelGrid.Instance != null ? 
            LevelGrid.Instance.GridToWorld(gridPosition, 0) : transform.position;
        
        Vector3 finalPosition = baseWorldPos + recipe.PositionOffset;
        Quaternion finalRotation = Quaternion.Euler(recipe.RotationOffset);
        
        Debug.Log($"[Blueprint] Creating visual for {recipe.RecipeName}");
        Debug.Log($"[Blueprint] Base position: {baseWorldPos}");
        Debug.Log($"[Blueprint] Recipe offset: {recipe.PositionOffset}");
        Debug.Log($"[Blueprint] Final position: {finalPosition}");
        Debug.Log($"[Blueprint] Recipe rotation: {recipe.RotationOffset}");
        
        blueprintVisual = Instantiate(recipe.ResultPrefab, finalPosition, finalRotation);
        blueprintVisual.name = $"Blueprint Visual ({recipe.RecipeName})";
        
        Debug.Log($"[Blueprint] Instantiated at: {blueprintVisual.transform.position}");
        
        // REMOVE NetworkObject component if it exists (causes duplicate ID errors)
        Unity.Netcode.NetworkObject netObj = blueprintVisual.GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null)
        {
            Debug.Log($"[Blueprint] Removing NetworkObject from blueprint visual to prevent duplicates");
            Destroy(netObj);
        }
        
        // DISABLE colliders (don't destroy - we'll re-enable later)
        Collider[] colliders = blueprintVisual.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
        
        // DISABLE rigidbody physics (don't destroy)
        Rigidbody rb = blueprintVisual.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
        
        // Store original materials and apply ghost material
        Renderer[] renderers = blueprintVisual.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            // Store original
            originalMaterials[rend] = rend.material;
            
            // Apply ghost material
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
    }

    void UpdateBlueprintVisual()
    {
        if (blueprintVisual != null)
        {
            float progress = GetCompletionPercentage();
            
            Renderer[] renderers = blueprintVisual.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                Color color = rend.material.color;
                color.a = 0.3f + (progress * 0.4f);
                rend.material.color = color;
            }
        }
    }

    public bool AcceptsTool(string toolType)
    {
        if (recipe == null) return false;
        return recipe.RequiresTool(toolType);
    }

    public bool ProcessMaterial(string toolType, MaterialType materialType)
    {
        if (recipe == null) return false;
        if (isComplete) return false;
        
        MaterialType? expectedMaterial = recipe.GetMaterialForTool(toolType);
        if (expectedMaterial == null || expectedMaterial != materialType)
        {
            return false;
        }
        
        int required = recipe.GetRequiredCount(materialType);
        int current = GetProcessedCount(materialType);
        
        if (current >= required)
        {
            return false;
        }
        
        IncrementProcessedCount(materialType);
        UpdateBlueprintVisual();
        CheckCompletion();
        
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
    }

    void CheckCompletion()
    {
        if (recipe == null) return;
        if (isComplete) return;
        
        foreach (var requirement in recipe.RequiredMaterials)
        {
            int processed = GetProcessedCount(requirement.materialType);
            if (processed < requirement.count)
            {
                return;
            }
        }
        
        CompleteRecipe();
    }

    void CompleteRecipe()
    {
        isComplete = true;
        Debug.Log($"[Blueprint] Recipe complete at {gridPosition}! Converting blueprint to real object");
        
        if (blueprintVisual == null) return;
        
        // RESTORE original materials (remove ghost effect)
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.material = kvp.Value;
            }
        }
        
        // ENABLE colliders
        Collider[] colliders = blueprintVisual.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = true;
        }
        
        // Ensure rigidbody is kinematic
        Rigidbody rb = blueprintVisual.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        
        // Get or add PlaceableObject component
        PlaceableObject placeable = blueprintVisual.GetComponent<PlaceableObject>();
        if (placeable != null)
        {
            // SAVE current transform (already positioned correctly by recipe offsets)
            Vector3 correctPosition = blueprintVisual.transform.position;
            Quaternion correctRotation = blueprintVisual.transform.rotation;
            
            // Make sure it's registered with grid and refined
            placeable.OnPlaced(gridPosition);
            placeable.Refine();
            
            // RESTORE correct transform (OnPlaced resets it)
            blueprintVisual.transform.position = correctPosition;
            blueprintVisual.transform.rotation = correctRotation;
            
            Debug.Log($"[Blueprint] Converted to Refined object at {gridPosition}");
        }
        
        // DON'T add NetworkObject - blueprint visual is scene object, doesn't need network sync
        // The CellBlueprint itself is a scene object, and the visual just follows its state
        
        // Rename for clarity
        blueprintVisual.name = recipe.RecipeName + " (Complete)";
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
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(worldPos + Vector3.up * 2, 
            $"{recipe.RecipeName}\n{GetCompletionPercentage() * 100:F0}%");
        #endif
    }
}