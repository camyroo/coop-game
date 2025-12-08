using UnityEngine;

public static class GameObjectUtils
{
    // Change material on a GameObject
    public static void ChangeMaterial(GameObject obj, Material newMaterial)
    {
        if (obj == null || newMaterial == null) return;
        
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = newMaterial;
        }
    }

    // Overload for direct Renderer usage
    public static void ChangeMaterial(Renderer renderer, Material newMaterial)
    {
        if (renderer != null && newMaterial != null)
        {
            renderer.material = newMaterial;
        }
    }

    
    public static void SetActive(GameObject obj, bool active)
    {
        if (obj != null) obj.SetActive(active);
    }

    public static T GetOrAddComponent<T>(GameObject obj) where T : Component
    {
        T component = obj.GetComponent<T>();
        if (component == null)
        {
            component = obj.AddComponent<T>();
        }
        return component;
    }
    

}