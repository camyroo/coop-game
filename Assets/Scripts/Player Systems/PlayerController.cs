using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Player Identity")]
    public NetworkVariable<int> playerNumber = new NetworkVariable<int>();
    public NetworkVariable<Color> playerColor = new NetworkVariable<Color>(new Color(1f, 0.5f, 0f, 1f)); // Orange
    private string playerName;
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI nameText;
    
    private Renderer playerRenderer;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        playerRenderer = GetComponent<Renderer>();
        
        // Subscribe to network variable changes
        playerColor.OnValueChanged += OnColorChanged;
        
        // Apply initial values
        ApplyVisuals();
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        playerColor.OnValueChanged -= OnColorChanged;
    }
    
    public void Initialize(int number, string name, Color color)
    {
        if (!IsServer) return;
        
        playerNumber.Value = number;
        playerName = name;
        playerColor.Value = color;
        
        ApplyVisuals();
    }
    
    void ApplyVisuals()
    {
        // Debug.Log($"ApplyVisuals called. PlayerColor: {playerColor.Value}, Renderer: {playerRenderer != null}");
        
        // if (playerRenderer != null)
        // {
        //     playerRenderer.material.color = playerColor.Value;
        //     Debug.Log($"Set material color to: {playerRenderer.material.color}");
        // }
        
        UpdateNameText();
    }
    
    void OnColorChanged(Color oldColor, Color newColor)
    {
        // if (playerRenderer != null)
        // {
        //     playerRenderer.material.color = newColor;
        // }
    }
    
    void UpdateNameText()
    {
        if (nameText != null)
        {
            nameText.text = !string.IsNullOrEmpty(playerName) 
                ? playerName 
                : $"Player {playerNumber.Value}";
        }
    }
    
    // Public getters
    public int GetPlayerNumber() => playerNumber.Value;
    public Color GetPlayerColor() => playerColor.Value;
    public string GetPlayerName() => playerName;
}