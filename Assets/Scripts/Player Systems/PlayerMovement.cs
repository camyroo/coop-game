using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 15f;
    
    private Rigidbody rb;
    private PlayerInput playerInput;
    private Vector3 currentVelocity;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        
        rb.freezeRotation = true;
        
        if (!IsOwner)
        {
            rb.isKinematic = true;
        }
    }
    
    void FixedUpdate()
    {
        if (!IsOwner) return;
        
        HandleMovement();
        HandleRotation();
    }
    
    void HandleMovement()
    {
        Vector2 input = playerInput.MoveInput;
        Vector3 targetVelocity = new Vector3(input.x, 0f, input.y) * moveSpeed;
        
        // Smooth acceleration/deceleration
        float rate = targetVelocity.magnitude > 0.1f ? acceleration : deceleration;
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        
        // Apply movement
        Vector3 newPosition = rb.position + currentVelocity * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);
    }
    
    void HandleRotation()
    {
        Vector2 input = playerInput.MoveInput;
        
        if (input.magnitude > 0.1f)
        {
            Vector3 direction = new Vector3(input.x, 0f, input.y);
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }
    
    public Vector3 GetVelocity() => currentVelocity;
    public bool IsMoving() => currentVelocity.magnitude > 0.1f;
}