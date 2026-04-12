using UnityEngine;

public class PickupTrigger : MonoBehaviour
{
    public CarryableItem item;

    void Awake()
    {
        if(item == null) item = GetComponent<CarryableItem>();
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<SimplePlayerController>();
        if(player == null) return;

        if(item != null && player.PickupItem(item))
        {
            // Optional: play pickup SFX/VFX
        }
    }
}
