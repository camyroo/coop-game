using UnityEngine;

public class TriggerZone : MonoBehaviour
{
    public Material red;
    public Material green;

    private Renderer objectRenderer;

    void Start() 
    {
        objectRenderer = GetComponent<Renderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger Entered by: " + other.gameObject.name);

        if (other.CompareTag("Player"))
        {
            Debug.Log("The Player has entered the zone!");
            GameObjectUtils.ChangeMaterial(objectRenderer, green);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Trigger Exited by: " + other.gameObject.name);

        if (other.CompareTag("Player"))
        {
            Debug.Log("The Player has exited the zone!");
            GameObjectUtils.ChangeMaterial(objectRenderer, red);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Debug.Log("Staying in the trigger zone: " + other.gameObject.name);
    }
}