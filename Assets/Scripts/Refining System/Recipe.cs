using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Defines a recipe for building an object
/// Example: Concrete Wall = 3x Concrete
/// Example: Reinforced Wall = 1x Steel + 1x Concrete
/// </summary>
[CreateAssetMenu(fileName = "New Recipe", menuName = "Construction/Recipe")]
public class Recipe : ScriptableObject
{
    [Header("Recipe Definition")]
    [SerializeField] private string recipeName = "Concrete Wall";
    [SerializeField] private List<MaterialRequirement> requiredMaterials = new List<MaterialRequirement>();
    
    [Header("Result")]
    [SerializeField] private GameObject resultPrefab; // What spawns when recipe completes
    [SerializeField] private GridLayer targetLayer = GridLayer.Wall;
    
    [Header("Transform")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero; // Y offset for spawning above ground
    [SerializeField] private Vector3 rotationOffset = Vector3.zero; // Euler angles for rotation
    
    [Header("Visuals")]
    [SerializeField] private Material blueprintMaterial; // Ghost material showing what will be built
    
    public string RecipeName => recipeName;
    public List<MaterialRequirement> RequiredMaterials => requiredMaterials;
    public GameObject ResultPrefab => resultPrefab;
    public GridLayer TargetLayer => targetLayer;
    public Material BlueprintMaterial => blueprintMaterial;
    public Vector3 PositionOffset => positionOffset;
    public Vector3 RotationOffset => rotationOffset;
    
    /// <summary>
    /// Get total number of materials needed
    /// </summary>
    public int GetTotalMaterialCount()
    {
        return requiredMaterials.Sum(m => m.count);
    }
    
    /// <summary>
    /// Get count needed for specific material type
    /// </summary>
    public int GetRequiredCount(MaterialType type)
    {
        var requirement = requiredMaterials.FirstOrDefault(m => m.materialType == type);
        return requirement?.count ?? 0;
    }
    
    /// <summary>
    /// Get all unique tool types needed for this recipe
    /// </summary>
    public List<string> GetRequiredToolTypes()
    {
        return requiredMaterials.Select(m => m.toolType).Distinct().ToList();
    }
    
    /// <summary>
    /// Check if a tool type is required for this recipe
    /// </summary>
    public bool RequiresTool(string toolType)
    {
        return requiredMaterials.Any(m => m.toolType == toolType);
    }
    
    /// <summary>
    /// Get which material type uses this tool
    /// </summary>
    public MaterialType? GetMaterialForTool(string toolType)
    {
        var requirement = requiredMaterials.FirstOrDefault(m => m.toolType == toolType);
        return requirement?.materialType;
    }
}

/// <summary>
/// Single material requirement in a recipe
/// </summary>
[System.Serializable]
public class MaterialRequirement
{
    public MaterialType materialType;
    public int count;
    public string toolType; // Which tool processes this material (e.g., "concrete", "steel")
}