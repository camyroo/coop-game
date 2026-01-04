using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInteraction : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableLayer;

    private PlayerInput playerInput;
    private IInteractable currentInteractable;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        playerInput = GetComponent<PlayerInput>();
    }

    void Update()
    {
        if (!IsOwner) return;

        CheckForInteractable();

        if (playerInput.InteractPressed && currentInteractable != null)
        {
            TryInteract();
        }
    }

    void CheckForInteractable()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, interactionRange, interactableLayer);
        
        IInteractable closest = null;
        float closestDist = interactionRange;

        foreach (Collider col in nearbyObjects)
        {
            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable != null)
            {
                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < closestDist)
                {
                    closest = interactable;
                    closestDist = dist;
                }
            }
        }

        currentInteractable = closest;
    }

    void TryInteract()
    {
        if (currentInteractable == null) return;

        // If it's a spawner station, check if player should be grabbing instead
        if (currentInteractable is SpawnerStation)
        {
            PlayerGrabSystem grabSystem = GetComponent<PlayerGrabSystem>();
            if (grabSystem != null && !grabSystem.CanInteractWithStation())
            {
                // There's a nearby object to grab, don't interact with station
                return;
            }
        }

        NetworkObject netObj = (currentInteractable as Component)?.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            InteractServerRpc(netObj.NetworkObjectId);
        }
    }

    [ServerRpc]
    void InteractServerRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            IInteractable interactable = netObj.GetComponent<IInteractable>();
            PlayerController playerController = GetComponent<PlayerController>();
            
            if (interactable != null && interactable.CanInteract(playerController))
            {
                interactable.Interact(playerController);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}