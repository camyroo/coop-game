using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInput : NetworkBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool GrabPressed { get; private set; }
    public bool PlacePressed { get; private set; }
    public bool DropPressed { get; private set; }
    
    private InputAction moveAction;
    private InputAction interactAction;
    private InputAction grabAction;
    private InputAction placeAction;
    private InputAction dropAction;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (!IsOwner) return;
        
        SetupInput();
    }
    
    void SetupInput()
    {
        // Movement
        moveAction = new InputAction(binding: "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        
        // Actions
        interactAction = new InputAction(binding: "<Keyboard>/e");
        grabAction = new InputAction(binding: "<Keyboard>/f");
        placeAction = new InputAction(binding: "<Keyboard>/e");
        dropAction = new InputAction(binding: "<Keyboard>/q");
        
        // Enable all
        moveAction.Enable();
        interactAction.Enable();
        grabAction.Enable();
        placeAction.Enable();
        dropAction.Enable();
    }
    
    void Update()
    {
        if (!IsOwner) return;
        
        ReadInput();
    }
    
    void ReadInput()
    {
        MoveInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        InteractPressed = interactAction?.WasPressedThisFrame() ?? false;
        GrabPressed = grabAction?.WasPressedThisFrame() ?? false;
        PlacePressed = placeAction?.WasPressedThisFrame() ?? false;
        DropPressed = dropAction?.WasPressedThisFrame() ?? false;
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        moveAction?.Disable();
        interactAction?.Disable();
        grabAction?.Disable();
        placeAction?.Disable();
        dropAction?.Disable();
    }
}