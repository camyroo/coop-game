using UnityEngine;
using Unity.Netcode;
using Steamworks;
using Steamworks.Data;
using Netcode.Transports.Facepunch;
using System;

public class NetworkingManager : MonoBehaviour
{
    public static NetworkingManager Instance { get; private set; }
    
    private FacepunchTransport transport;
    private Lobby? currentLobby;
    private string currentJoinCode;
    
    // Events for UI to subscribe to
    public event Action<int> OnPlayerCountChanged;
    public event Action<string> OnJoinCodeGenerated;
    public event Action<bool> OnConnectionStatusChanged;
    
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
    
    void Start()
    {
        transport = NetworkManager.Singleton.GetComponent<FacepunchTransport>();
        
        // Subscribe to network events
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }
    
    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    
    #region Lobby Management
    
    public async void CreateLobby(int maxPlayers = 4)
    {
        var createLobbyOutput = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
        
        if (!createLobbyOutput.HasValue)
        {
            Debug.LogError("Failed to create lobby");
            return;
        }

        currentLobby = createLobbyOutput.Value;
        currentJoinCode = GenerateJoinCode();
        
        currentLobby.Value.SetPublic();
        currentLobby.Value.SetData("name", "Party Game");
        currentLobby.Value.SetJoinable(true);
        currentLobby.Value.SetData("HostSteamID", SteamClient.SteamId.ToString());
        currentLobby.Value.SetData("JoinCode", currentJoinCode);
        
        Debug.Log($"Lobby created! Join Code: {currentJoinCode}");
        OnJoinCodeGenerated?.Invoke(currentJoinCode);
        
        await System.Threading.Tasks.Task.Delay(100);
        
        transport.targetSteamId = SteamClient.SteamId;
        NetworkManager.Singleton.StartHost();
        
        OnConnectionStatusChanged?.Invoke(true);
    }
    
    public async void JoinLobbyByCode(string code)
    {
        code = code.ToUpper().Trim();
        Debug.Log($"Searching for lobby with code: {code}");
        
        var lobbies = await SteamMatchmaking.LobbyList
            .WithMaxResults(50)
            .RequestAsync();
        
        if (lobbies != null)
        {
            foreach (var lobby in lobbies)
            {
                string lobbyCode = lobby.GetData("JoinCode");
                if (lobbyCode == code)
                {
                    Debug.Log($"Found lobby with code {code}!");
                    await SteamMatchmaking.JoinLobbyAsync(lobby.Id);
                    return;
                }
            }
        }
        
        Debug.LogError($"No lobby found with code: {code}");
    }
    
    public void LeaveLobby()
    {
        if (currentLobby.HasValue)
        {
            currentLobby.Value.Leave();
            currentLobby = null;
        }
        
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        OnConnectionStatusChanged?.Invoke(false);
    }
    
    public string GetCurrentJoinCode() => currentJoinCode;
    public bool IsConnected() => NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer;
    public int GetPlayerCount() => NetworkManager.Singleton.ConnectedClients.Count;
    
    #endregion
    
    #region Helper Methods
    
    private string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        System.Random random = new System.Random();
        char[] code = new char[5];
        
        for (int i = 0; i < 5; i++)
        {
            code[i] = chars[random.Next(chars.Length)];
        }
        
        return new string(code);
    }
    
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected");
        OnPlayerCountChanged?.Invoke(NetworkManager.Singleton.ConnectedClients.Count);
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");
        OnPlayerCountChanged?.Invoke(NetworkManager.Singleton.ConnectedClients.Count);
    }
    
    #endregion
}