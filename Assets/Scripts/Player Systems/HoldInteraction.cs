using UnityEngine;
using System;

/// <summary>
/// Handles hold-to-interact mechanics with progress tracking
/// Attach to any object that needs hold interactions
/// </summary>
public class HoldInteraction : MonoBehaviour
{
    [Header("Hold Settings")]
    [SerializeField] private float holdDuration = 2f;
    
    private bool isHolding = false;
    private float holdProgress = 0f;
    private Action onCompleteCallback;
    private Action<float> onProgressCallback;
    private Action onCancelCallback;
    
    public bool IsHolding => isHolding;
    public float HoldProgress => holdProgress;
    public float HoldDuration => holdDuration;
    
    void Update()
    {
        if (!isHolding) return;
        
        // Increase progress
        holdProgress += Time.deltaTime;
        
        // Notify progress
        onProgressCallback?.Invoke(holdProgress / holdDuration);
        
        // Check if complete
        if (holdProgress >= holdDuration)
        {
            Complete();
        }
    }
    
    /// <summary>
    /// Start a hold interaction
    /// </summary>
    public void StartHold(float duration, Action onComplete, Action<float> onProgress = null, Action onCancel = null)
    {
        if (isHolding)
        {
            Debug.LogWarning("[HoldInteraction] Already holding, canceling previous hold");
            Cancel();
        }
        
        holdDuration = duration;
        holdProgress = 0f;
        isHolding = true;
        onCompleteCallback = onComplete;
        onProgressCallback = onProgress;
        onCancelCallback = onCancel;
        
        Debug.Log($"[HoldInteraction] Started hold for {duration}s");
    }
    
    /// <summary>
    /// Cancel the current hold (called when button released early)
    /// </summary>
    public void Cancel()
    {
        if (!isHolding) return;
        
        Debug.Log($"[HoldInteraction] Hold canceled at {holdProgress:F2}s / {holdDuration}s");
        
        isHolding = false;
        holdProgress = 0f;
        
        onCancelCallback?.Invoke();
        
        onCompleteCallback = null;
        onProgressCallback = null;
        onCancelCallback = null;
    }
    
    /// <summary>
    /// Complete the hold interaction
    /// </summary>
    void Complete()
    {
        Debug.Log($"[HoldInteraction] Hold completed!");
        
        isHolding = false;
        holdProgress = 0f;
        
        onCompleteCallback?.Invoke();
        
        onCompleteCallback = null;
        onProgressCallback = null;
        onCancelCallback = null;
    }
    
    /// <summary>
    /// Force complete immediately (for testing or special cases)
    /// </summary>
    public void ForceComplete()
    {
        if (!isHolding) return;
        
        holdProgress = holdDuration;
        Complete();
    }
}