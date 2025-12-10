using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button startGameButton;
    
    void Start()
    {
        // Subscribe to game state events
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnStateChanged;
            GameStateManager.Instance.OnTimeUpdate += OnTimeUpdate;
            GameStateManager.Instance.OnRoundChanged += OnRoundChanged;
        }
        
        if (NetworkingManager.Instance != null)
        {
            NetworkingManager.Instance.OnPlayerCountChanged += OnPlayerCountChanged;
        }
        
        // Setup button
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGameClicked);
        }
        
        UpdateUI();
    }
    
    void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnStateChanged;
            GameStateManager.Instance.OnTimeUpdate -= OnTimeUpdate;
            GameStateManager.Instance.OnRoundChanged -= OnRoundChanged;
        }
        
        if (NetworkingManager.Instance != null)
        {
            NetworkingManager.Instance.OnPlayerCountChanged -= OnPlayerCountChanged;
        }
    }
    
    #region Event Handlers
    
    void OnStateChanged(GameState oldState, GameState newState)
    {
        UpdateUI();
        
        switch (newState)
        {
            case GameState.Playing:
                ShowGameUI();
                break;
            case GameState.GameOver:
                ShowGameOverUI();
                break;
            case GameState.Lobby:
                ShowLobbyUI();
                break;
        }
    }
    
    void OnTimeUpdate(float time)
    {
        if (timerText != null && GameStateManager.Instance != null)
        {
            float remaining = GameStateManager.Instance.GetRemainingTime();
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }
    
    void OnRoundChanged(int round)
    {
        if (roundText != null && GameStateManager.Instance != null)
        {
            roundText.text = $"Round {round}/{GameStateManager.Instance.GetMaxRounds()}";
        }
    }
    
    void OnPlayerCountChanged(int count)
    {
        if (playerCountText != null)
        {
            playerCountText.text = $"Players: {count}";
        }
    }
    
    void OnStartGameClicked()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.RequestStartGameServerRpc();
        }
    }
    
    #endregion
    
    #region UI State Management
    
    void UpdateUI()
    {
        if (GameStateManager.Instance == null) return;
        
        GameState state = GameStateManager.Instance.CurrentState.Value;
        
        // Show/hide start button
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(state == GameState.Lobby);
        }
        
        // Show/hide game over panel
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(state == GameState.GameOver);
        }
    }
    
    void ShowGameUI()
    {
        if (timerText != null) timerText.gameObject.SetActive(true);
        if (roundText != null) roundText.gameObject.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }
    
    void ShowGameOverUI()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (timerText != null) timerText.gameObject.SetActive(false);
    }
    
    void ShowLobbyUI()
    {
        if (timerText != null) timerText.gameObject.SetActive(false);
        if (roundText != null) roundText.gameObject.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }
    
    #endregion
}