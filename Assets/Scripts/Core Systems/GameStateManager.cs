using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;

public enum GameState
{
    Menu,
    Lobby,
    Playing,
    Paused,
    GameOver
}

public class GameStateManager : NetworkBehaviour
{
    public static GameStateManager Instance { get; private set; }
    
    [Header("Game State")]
    public NetworkVariable<GameState> CurrentState = new NetworkVariable<GameState>(GameState.Menu);
    public NetworkVariable<float> GameTime = new NetworkVariable<float>(0f);
    public NetworkVariable<int> RoundNumber = new NetworkVariable<int>(1);
    
    [Header("Settings")]
    [SerializeField] private float roundDuration = 300f; // 5 minutes
    [SerializeField] private int maxRounds = 3;
    
    // Events
    public event Action<GameState, GameState> OnStateChanged;
    public event Action<int> OnRoundChanged;
    public event Action<float> OnTimeUpdate;
    public event Action OnGameStart;
    public event Action OnGameEnd;
    
    private float localTimer;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        CurrentState.OnValueChanged += OnStateValueChanged;
        RoundNumber.OnValueChanged += OnRoundValueChanged;
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        CurrentState.OnValueChanged -= OnStateValueChanged;
        RoundNumber.OnValueChanged -= OnRoundValueChanged;
    }
    
    void Update()
    {
        if (!IsServer) return;
        
        if (CurrentState.Value == GameState.Playing)
        {
            UpdateGameTimer();
        }
    }
    
    #region State Management
    
    public void ChangeState(GameState newState)
    {
        if (!IsServer) return;
        
        GameState oldState = CurrentState.Value;
        CurrentState.Value = newState;
        
        OnStateChanged?.Invoke(oldState, newState);
        
        HandleStateTransition(newState);
    }
    
    void OnStateValueChanged(GameState oldState, GameState newState)
    {
        OnStateChanged?.Invoke(oldState, newState);
    }
    
    void HandleStateTransition(GameState newState)
    {
        switch (newState)
        {
            case GameState.Playing:
                StartGame();
                break;
            case GameState.GameOver:
                EndGame();
                break;
            case GameState.Lobby:
                ResetGame();
                break;
        }
    }
    
    #endregion
    
    #region Game Flow
    
    void StartGame()
    {
        if (!IsServer) return;
        
        GameTime.Value = 0f;
        localTimer = 0f;
        
        OnGameStart?.Invoke();
        Debug.Log("Game Started!");
    }
    
    void EndGame()
    {
        if (!IsServer) return;
        
        OnGameEnd?.Invoke();
        Debug.Log("Game Ended!");
    }
    
    void ResetGame()
    {
        if (!IsServer) return;
        
        GameTime.Value = 0f;
        RoundNumber.Value = 1;
        localTimer = 0f;
    }
    
    void UpdateGameTimer()
    {
        localTimer += Time.deltaTime;
        GameTime.Value = localTimer;
        
        OnTimeUpdate?.Invoke(localTimer);
        
        // Check if round is over
        if (localTimer >= roundDuration)
        {
            EndRound();
        }
    }
    
    void EndRound()
    {
        if (RoundNumber.Value >= maxRounds)
        {
            ChangeState(GameState.GameOver);
        }
        else
        {
            StartNextRound();
        }
    }
    
    void StartNextRound()
    {
        RoundNumber.Value++;
        localTimer = 0f;
        GameTime.Value = 0f;
    }
    
    void OnRoundValueChanged(int oldRound, int newRound)
    {
        OnRoundChanged?.Invoke(newRound);
    }
    
    #endregion
    
    #region Public API
    
    public bool IsPlaying() => CurrentState.Value == GameState.Playing;
    public bool IsInLobby() => CurrentState.Value == GameState.Lobby;
    public float GetRemainingTime() => Mathf.Max(0, roundDuration - GameTime.Value);
    public int GetCurrentRound() => RoundNumber.Value;
    public int GetMaxRounds() => maxRounds;
    
    [ServerRpc(RequireOwnership = false)]
    public void RequestStartGameServerRpc()
    {
        if (CurrentState.Value == GameState.Lobby)
        {
            ChangeState(GameState.Playing);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void RequestEndGameServerRpc()
    {
        ChangeState(GameState.GameOver);
    }
    
    #endregion
}