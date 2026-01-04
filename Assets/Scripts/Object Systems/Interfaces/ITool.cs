using UnityEngine;

public interface ITool
{
    void OnEquipped(Transform holdPoint);
    void OnUnequipped();
    void OnUse(GameObject target);
    bool CanBeUsed();
    string GetToolName();
}