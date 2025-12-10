using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/Config")]
public class GameConfig : ScriptableObject
{
    [Header("Network Settings")]
    public int maxPlayers = 4;
    public float tickRate = 60f;
    
    [Header("Game Rules")]
    public float roundDuration = 300f;
    public int maxRounds = 3;
    public bool friendlyFire = false;
    
    [Header("Player Settings")]
    public float playerMoveSpeed = 5f;
    public float playerRotationSpeed = 10f;
    public float grabDistance = 2f;
    
    [Header("Object Settings")]
    public int maxPlaceableObjects = 50;
    public float objectHoldForce = 500f;
    public float objectHoldDrag = 10f;
    
    [Header("Grid Settings")]
    public float gridTileSize = 2f;
    public Vector2Int gridSize = new Vector2Int(20, 20);
}