using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public Transform[] spawnPoints;
    
    public Color[] playerColors = new Color[]
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow
    };
    
    void Start()
    {
        SpawnPlayers(1); 
    }
    
    void SpawnPlayers(int numberOfPlayers)
    {
        for (int i = 0; i < numberOfPlayers; i++)
        {
            Vector3 spawnPos = spawnPoints[i].position;
            GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            
            PlayerController playerController = player.GetComponent<PlayerController>();
            playerController.Initialize(i + 1, "Player " + (i + 1), playerColors[i]);
        }
    }
}