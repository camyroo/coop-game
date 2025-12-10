using UnityEngine;

public class MenuUI : MonoBehaviour
{
    private string joinCodeInput = "";
    
    void Start()
    {
        // Subscribe to networking events
        NetworkingManager.Instance.OnJoinCodeGenerated += OnJoinCodeGenerated;
        NetworkingManager.Instance.OnPlayerCountChanged += OnPlayerCountChanged;
        NetworkingManager.Instance.OnConnectionStatusChanged += OnConnectionStatusChanged;
    }
    
    void OnDestroy()
    {
        if (NetworkingManager.Instance != null)
        {
            NetworkingManager.Instance.OnJoinCodeGenerated -= OnJoinCodeGenerated;
            NetworkingManager.Instance.OnPlayerCountChanged -= OnPlayerCountChanged;
            NetworkingManager.Instance.OnConnectionStatusChanged -= OnConnectionStatusChanged;
        }
    }
    
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, 400));
        
        if (!NetworkingManager.Instance.IsConnected())
        {
            DrawMainMenu();
        }
        else
        {
            DrawConnectedMenu();
        }
        
        GUILayout.EndArea();
    }
    
    void DrawMainMenu()
    {
        if (GUILayout.Button("Host Game"))
        {
            NetworkingManager.Instance.CreateLobby();
        }
        
        GUILayout.Space(20);
        GUILayout.Label("Join Code:");
        joinCodeInput = GUILayout.TextField(joinCodeInput.ToUpper(), GUILayout.Width(200));
        
        if (GUILayout.Button("Join Game"))
        {
            if (!string.IsNullOrEmpty(joinCodeInput))
            {
                NetworkingManager.Instance.JoinLobbyByCode(joinCodeInput);
            }
        }
    }
    
    void DrawConnectedMenu()
    {
        GUILayout.Label($"Players: {NetworkingManager.Instance.GetPlayerCount()}");
        
        string code = NetworkingManager.Instance.GetCurrentJoinCode();
        if (!string.IsNullOrEmpty(code))
        {
            GUILayout.Label($"Join Code: {code}");
            
            if (GUILayout.Button("Copy Code"))
            {
                GUIUtility.systemCopyBuffer = code;
            }
        }
        
        if (GUILayout.Button("Disconnect"))
        {
            NetworkingManager.Instance.LeaveLobby();
        }
    }
    
    // Event callbacks
    void OnJoinCodeGenerated(string code)
    {
        Debug.Log($"Join code: {code}");
    }
    
    void OnPlayerCountChanged(int count)
    {
        Debug.Log($"Player count: {count}");
    }
    
    void OnConnectionStatusChanged(bool connected)
    {
        Debug.Log($"Connection status: {connected}");
    }
}