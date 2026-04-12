using UnityEngine;

public class WaterVolume : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponentInParent<SimplePlayerController>();
        if(pc) pc.EnterWater();
    }

    private void OnTriggerExit(Collider other)
    {
        var pc = other.GetComponentInParent<SimplePlayerController>();
        if(pc) pc.ExitWater();
    }
}