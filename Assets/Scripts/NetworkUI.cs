using UnityEngine;
using Unity.Netcode;
using Steamworks;
using Steamworks.Data;
using Netcode.Transports.Facepunch;
using System.Collections.Generic;

public class NetworkUI : MonoBehaviour
{
    private Lobby? currentLobby;
    private string joinCodeInput = "";
    private string currentJoinCode = "";
    private FacepunchTransport transport;
    private bool isConnecting = false;
    
    // Dictionary to map codes to lobby IDs
    private static Dictionary<string, ulong> activeCodes = new Dictionary<string, ulong>();

    void Start()
    {
        transport = NetworkManager.Singleton.GetComponent<FacepunchTransport>();
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, 400));
        
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Host Game (Create Lobby)"))
            {
                CreateLobby();
            }
            
            GUILayout.Space(20);
            GUILayout.Label("Join Code:");
            joinCodeInput = GUILayout.TextField(joinCodeInput.ToUpper(), GUILayout.Width(200));
            
            if (GUILayout.Button("Join Game") && !isConnecting)
            {
                if (!string.IsNullOrEmpty(joinCodeInput))
                {
                    JoinByCode(joinCodeInput);
                }
            }
            
            GUILayout.Space(20);
            if (GUILayout.Button("Find Friend's Lobby") && !isConnecting)
            {
                FindFriendLobbies();
            }
        }
        else
        {
            GUILayout.Label($"Connected! Players: {NetworkManager.Singleton.ConnectedClients.Count}");
            
            if (currentLobby.HasValue && !string.IsNullOrEmpty(currentJoinCode))
            {
                GUILayout.Label($"Join Code: {currentJoinCode}");
                
                if (GUILayout.Button("Copy Join Code"))
                {
                    GUIUtility.systemCopyBuffer = currentJoinCode;
                    Debug.Log($"Copied join code: {currentJoinCode}");
                }
                
                if (GUILayout.Button("Invite Friends"))
                {
                    SteamFriends.OpenGameInviteOverlay(currentLobby.Value.Id);
                }
            }
            
            if (GUILayout.Button("Disconnect"))
            {
                Disconnect();
            }
        }
        
        GUILayout.EndArea();
    }

    string GenerateJoinCode()
    {
        // Generate a 5-character code using letters and numbers
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Removed confusing chars like I, O, 0, 1
        System.Random random = new System.Random();
        char[] code = new char[5];
        
        for (int i = 0; i < 5; i++)
        {
            code[i] = chars[random.Next(chars.Length)];
        }
        
        return new string(code);
    }

    async void CreateLobby()
    {
        var createLobbyOutput = await SteamMatchmaking.CreateLobbyAsync(4);
        
        if (!createLobbyOutput.HasValue)
        {
            Debug.LogError("Failed to create lobby");
            return;
        }

        currentLobby = createLobbyOutput.Value;
        
        // Generate a simple join code
        currentJoinCode = GenerateJoinCode();
        
        // Store the lobby ID in the lobby metadata
        currentLobby.Value.SetPublic();
        currentLobby.Value.SetData("name", "My Party Game");
        currentLobby.Value.SetJoinable(true);
        currentLobby.Value.SetData("HostSteamID", SteamClient.SteamId.ToString());
        currentLobby.Value.SetData("JoinCode", currentJoinCode);
        
        // Store locally for quick lookup
        activeCodes[currentJoinCode] = currentLobby.Value.Id.Value;
        
        Debug.Log($"Lobby created! Join Code: {currentJoinCode}");
        
        await System.Threading.Tasks.Task.Delay(100);
        
        transport.targetSteamId = SteamClient.SteamId;
        NetworkManager.Singleton.StartHost();
    }
    
    async void JoinByCode(string code)
    {
        if (isConnecting) return;
        isConnecting = true;
        
        code = code.ToUpper().Trim();
        Debug.Log($"Searching for lobby with code: {code}");
        
        // Search all public lobbies for one with this join code
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
        isConnecting = false;
    }
    
    async void FindFriendLobbies()
    {
        if (isConnecting) return;
        
        Debug.Log("Searching for friend lobbies...");
        
        var lobbies = await SteamMatchmaking.LobbyList
            .WithMaxResults(10)
            .RequestAsync();
            
        if (lobbies != null && lobbies.Length > 0)
        {
            Debug.Log($"Found {lobbies.Length} lobbies, joining first one");
            var lobby = lobbies[0];
            await SteamMatchmaking.JoinLobbyAsync(lobby.Id);
        }
        else
        {
            Debug.Log("No lobbies found");
        }
    }
    
    void Disconnect()
    {
        if (currentLobby.HasValue)
        {
            // Clean up the code mapping
            if (!string.IsNullOrEmpty(currentJoinCode) && activeCodes.ContainsKey(currentJoinCode))
            {
                activeCodes.Remove(currentJoinCode);
            }
            
            currentLobby.Value.Leave();
            currentLobby = null;
        }
        
        currentJoinCode = "";
        
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        isConnecting = false;
    }

    void OnEnable()
    {
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
    }

    void OnDisable()
    {
        SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
    }

    void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Debug.Log($"{friend.Name} joined the lobby");
    }
    
    void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        Debug.Log($"{friend.Name} left the lobby");
    }

    async void OnLobbyEntered(Lobby lobby)
    {
        currentLobby = lobby;
        
        if (NetworkManager.Singleton.IsHost)
        {
            isConnecting = false;
            return;
        }
        
        Debug.Log("Entered lobby as client, preparing to connect...");
        
        await System.Threading.Tasks.Task.Delay(200);
        
        string hostSteamIdString = lobby.GetData("HostSteamID");
        
        if (ulong.TryParse(hostSteamIdString, out ulong hostSteamId))
        {
            Debug.Log($"Connecting to host Steam ID: {hostSteamId}");
            transport.targetSteamId = hostSteamId;
            
            await System.Threading.Tasks.Task.Delay(100);
            
            NetworkManager.Singleton.StartClient();
            isConnecting = false;
        }
        else
        {
            Debug.LogError("Failed to get host Steam ID from lobby data");
            isConnecting = false;
        }
    }
}