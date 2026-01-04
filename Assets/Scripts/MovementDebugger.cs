using UnityEngine;
using Unity.Netcode;

public class SimpleMovementTest : NetworkBehaviour
{
    private Rigidbody rb;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    
    void FixedUpdate()
    {
        if (!IsOwner) 
        {
            Debug.Log("Not owner, skipping");
            return;
        }
        
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        
        if (keyboard == null)
        {
            Debug.LogError("KEYBOARD IS NULL!");
            return;
        }
        
        // Ultra simple - just set velocity directly
        float h = 0;
        float v = 0;
        
        bool wPressed = keyboard.wKey.isPressed;
        bool sPressed = keyboard.sKey.isPressed;
        bool aPressed = keyboard.aKey.isPressed;
        bool dPressed = keyboard.dKey.isPressed;
        
        Debug.Log($"Keys - W:{wPressed} S:{sPressed} A:{aPressed} D:{dPressed}");
        
        if (wPressed) v = 1;
        if (sPressed) v = -1;
        if (aPressed) h = -1;
        if (dPressed) h = 1;
        
        Debug.Log($"Input values - h:{h} v:{v}");
        
        Vector3 velocity = new Vector3(h, 0, v) * 5f;
        
        Debug.Log($"Setting velocity to: {velocity}");
        rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        
        Debug.Log($"RB velocity is now: {rb.linearVelocity}");
        Debug.Log($"Position: {rb.position}");
    }
}