using UnityEngine;
public class PickupInteractable : MonoBehaviour, IInteractable
{
    IPickupable pickup;

    void Awake() => pickup = GetComponent<IPickupable>();

    public void Interact(SimplePlayerController player)
    {
        pickup?.Pickup(player);
    }
}