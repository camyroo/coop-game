using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Player Info")]
    public int playerNumber;
    public string playerName;
    public Color playerColor;
    
    [Header("UI")]
    public TextMeshProUGUI nameText; 
    
    [Header("Movement")]
    public float moveSpeed = 5f;
    
    private Rigidbody rb;
    private Vector3 movement;
    private Renderer playerRenderer;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        rb = GetComponent<Rigidbody>();
        playerRenderer = GetComponent<Renderer>();
        
        // Make non-owner rigidbodies kinematic so NetworkTransform can control them
        if (!IsOwner)
        {
            rb.isKinematic = true;
        }
        
        SetPlayerColor(playerColor);
        UpdateNameText();
    }
    
    public void SetPlayerColor(Color color)
    {
        playerColor = color;
        if (playerRenderer != null)
        {
            playerRenderer.material.color = color;
        }
    }
    
    public void Initialize(int number, string name, Color color)
    {
        playerNumber = number;
        playerName = name;
        SetPlayerColor(color);
        UpdateNameText();
    }
    
    void UpdateNameText()
    {
        if (nameText != null)
        {
            nameText.text = playerName;
        }
    }
    
    void Update()
    {
        // Only control your own player
        if (!IsOwner) return;
        
        movement = Vector3.zero;
        
        if (UnityEngine.InputSystem.Keyboard.current.wKey.isPressed)
            movement.z = 1;
        if (UnityEngine.InputSystem.Keyboard.current.sKey.isPressed)
            movement.z = -1;
        if (UnityEngine.InputSystem.Keyboard.current.dKey.isPressed)
            movement.x = 1;
        if (UnityEngine.InputSystem.Keyboard.current.aKey.isPressed)
            movement.x = -1;
    }
    
    void FixedUpdate()
    {
        if (!IsOwner) return;
        
        if (rb != null)
        {
            rb.MovePosition(rb.position + movement.normalized * moveSpeed * Time.fixedDeltaTime);
        }
    }
}