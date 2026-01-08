using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple radial progress indicator for hold interactions
/// Shows as a circle that fills up while holding
/// </summary>
[RequireComponent(typeof(Image))]
public class HoldProgressUI : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Color progressColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.3f);
    
    private Image progressImage;
    private HoldInteraction holdInteraction;
    private bool wasHolding = false;
    
    void Awake()
    {
        progressImage = GetComponent<Image>();
        progressImage.type = Image.Type.Filled;
        progressImage.fillMethod = Image.FillMethod.Radial360;
        progressImage.fillOrigin = (int)Image.Origin360.Top;
        progressImage.fillAmount = 0f;
        progressImage.color = progressColor;
        
        // Start hidden
        gameObject.SetActive(false);
    }
    
    public void SetHoldInteraction(HoldInteraction interaction)
    {
        holdInteraction = interaction;
    }
    
    void Update()
    {
        if (holdInteraction == null) return;
        
        // Show/hide based on hold state
        if (holdInteraction.IsHolding && !wasHolding)
        {
            // Just started holding
            gameObject.SetActive(true);
            progressImage.fillAmount = 0f;
        }
        else if (!holdInteraction.IsHolding && wasHolding)
        {
            // Just stopped holding
            gameObject.SetActive(false);
            progressImage.fillAmount = 0f;
        }
        
        // Update progress
        if (holdInteraction.IsHolding)
        {
            float progress = holdInteraction.HoldProgress / holdInteraction.HoldDuration;
            progressImage.fillAmount = progress;
        }
        
        wasHolding = holdInteraction.IsHolding;
    }
}