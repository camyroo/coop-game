using UnityEngine;
using Unity.Netcode;
using Steamworks;
using Steamworks.Data;
using Netcode.Transports.Facepunch;

public class NetworkUI : MonoBehaviour
{
    private Lobby? currentLobby;
    private string lobbyIdInput = "";
    private FacepunchTransport transport;

    void Start()
    {
        // Get reference to the transport
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
            GUILayout.Label("Join by Lobby ID:");
            lobbyIdInput = GUILayout.TextField(lobbyIdInput, GUILayout.Width(200));
            
            if (GUILayout.Button("Join Lobby by ID"))
            {
                if (ulong.TryParse(lobbyIdInput, out ulong lobbyId))
                {
                    JoinLobby(lobbyId);
                }
            }
            
            GUILayout.Space(20);
            if (GUILayout.Button("Find Friend's Lobby"))
            {
                FindFriendLobbies();
            }
        }
        else
        {
            GUILayout.Label($"Connected! Players: {NetworkManager.Singleton.ConnectedClients.Count}");
            
            if (currentLobby.HasValue)
            {
                GUILayout.Label($"Lobby ID: {currentLobby.Value.Id}");
                
                if (GUILayout.Button("Invite Friends"))
                {
                    SteamFriends.OpenGameInviteOverlay(currentLobby.Value.Id);
                }
            }
            
            if (GUILayout.Button("Disconnect"))
            {
                if (currentLobby.HasValue)
                {
                    currentLobby.Value.Leave();
                }
                NetworkManager.Singleton.Shutdown();
            }
        }
        
        GUILayout.EndArea();
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
        currentLobby.Value.SetPublic();
        currentLobby.Value.SetData("name", "My Party Game");
        currentLobby.Value.SetJoinable(true);
        
        // Store host Steam ID in lobby data
        currentLobby.Value.SetData("HostSteamID", SteamClient.SteamId.ToString());
        
        Debug.Log($"Lobby created! ID: {currentLobby.Value.Id}");
        
        // Set transport to use our Steam ID
        transport.targetSteamId = SteamClient.SteamId;
        
        NetworkManager.Singleton.StartHost();
    }
    
    async void JoinLobby(ulong lobbyId)
    {
        Debug.Log($"Attempting to join lobby: {lobbyId}");
        var joinOutput = await SteamMatchmaking.JoinLobbyAsync(new SteamId { Value = lobbyId });
        
        if (!joinOutput.HasValue)
        {
            Debug.LogError("Failed to join lobby");
            return;
        }
        
        Debug.Log($"Joined lobby: {lobbyId}");
    }
    
    async void FindFriendLobbies()
    {
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

    void OnEnable()
    {
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
    }

    void OnDisable()
    {
        SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
    }

    void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Debug.Log($"{friend.Name} joined the lobby");
    }

    void OnLobbyEntered(Lobby lobby)
    {
        currentLobby = lobby;
        
        if (NetworkManager.Singleton.IsHost) return;
        
        Debug.Log("Joining as client...");
        
        // Get the host's Steam ID from lobby data
        string hostSteamIdString = lobby.GetData("HostSteamID");
        
        if (ulong.TryParse(hostSteamIdString, out ulong hostSteamId))
        {
            Debug.Log($"Connecting to host: {hostSteamId}");
            transport.targetSteamId = hostSteamId;
            NetworkManager.Singleton.StartClient();
        }
        else
        {
            Debug.LogError("Failed to get host Steam ID from lobby");
        }
    }
}