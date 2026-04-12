using UnityEngine;

public class CarrySlots : MonoBehaviour
{
    [Tooltip("Slot transforms in fill order")]
    public Transform[] slots;

    public Transform GetSlot(int index)
    {
        if(slots == null || slots.Length == 0) return null;
        if(index < 0 || index >= slots.Length) return null;
        return slots[index];
    }
}