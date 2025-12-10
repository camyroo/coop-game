using UnityEngine;

public interface IGrabbable
{
    void OnGrabbed(Transform holdPoint);
    void OnDropped();
    void OnPlaced(Vector2Int gridPosition);
    bool CanBeGrabbed();
}