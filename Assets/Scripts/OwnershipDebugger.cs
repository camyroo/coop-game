using UnityEngine;
using Unity.Netcode;

public class OwnershipDebugger : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        Debug.LogError($"========= PLAYER SPAWNED =========");
        Debug.LogError($"GameObject: {gameObject.name}");
        Debug.LogError($"IsOwner: {IsOwner}");
        Debug.LogError($"IsServer: {IsServer}");
        Debug.LogError($"IsClient: {IsClient}");
        Debug.LogError($"OwnerClientId: {OwnerClientId}");
        Debug.LogError($"NetworkManager.LocalClientId: {NetworkManager.Singleton.LocalClientId}");
        Debug.LogError($"Match: {OwnerClientId == NetworkManager.Singleton.LocalClientId}");
        Debug.LogError($"==================================");
    }
    
    void Update()
    {
        // Press P to print ownership info
        if (UnityEngine.InputSystem.Keyboard.current != null && 
            UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
        {
            Debug.LogError($"========= OWNERSHIP CHECK =========");
            Debug.LogError($"IsOwner: {IsOwner}");
            Debug.LogError($"OwnerClientId: {OwnerClientId}");
            Debug.LogError($"LocalClientId: {NetworkManager.Singleton.LocalClientId}");
            
            // List all players
            var players = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
            Debug.LogError($"Total NetworkObjects in scene: {players.Length}");
            
            int playerCount = 0;
            foreach (var netObj in players)
            {
                if (netObj.CompareTag("Player"))
                {
                    playerCount++;
                    Debug.LogError($"  Player {playerCount}: Owner={netObj.OwnerClientId}, IsOwner={netObj.IsOwner}, Name={netObj.gameObject.name}");
                }
            }
            Debug.LogError($"==================================");
        }
    }
}