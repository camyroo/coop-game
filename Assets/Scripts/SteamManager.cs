using UnityEngine;
using Steamworks;

public class SteamManager : MonoBehaviour
{
    void Start()
    {
        // Check if already initialized by transport
        if (SteamClient.IsValid)
        {
            Debug.Log($"Steam already initialized! Playing as: {SteamClient.Name}");
            
            // Enable overlay
            if (SteamUtils.IsOverlayEnabled)
            {
                Debug.Log("Steam overlay is enabled");
            }
            else
            {
                Debug.LogWarning("Steam overlay is disabled - enable it in Steam settings");
            }
            
            return;
        }

        try
        {
            SteamClient.Init(480);
            Debug.Log($"Steam initialized! Playing as: {SteamClient.Name}");
            
            // Enable overlay
            if (SteamUtils.IsOverlayEnabled)
            {
                Debug.Log("Steam overlay is enabled");
            }
            else
            {
                Debug.LogWarning("Steam overlay is disabled");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize Steam: {e.Message}");
        }
    }

    void Update()
    {
        if (SteamClient.IsValid)
        {
            SteamClient.RunCallbacks();
        }
    }

    void OnApplicationQuit()
    {
        if (SteamClient.IsValid)
        {
            SteamClient.Shutdown();
        }
    }
}