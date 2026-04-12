using UnityEngine;

public class LadderVolume : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var p = other.GetComponentInParent<SimplePlayerController>();
        if(p == null) return;

        // Don't auto-grab while falling / dropping past it.
        // Only grab if grounded OR player is intentionally climbing up.
        bool wantsUp = p.LastVerticalIntent > 0.2f;
        bool grounded = p.IsGrounded;
        bool falling = p.VerticalVelocity < -0.1f && !grounded;

        if(falling && !wantsUp)
            return;

        p.SetOnLadder(true);
    }

    private void OnTriggerExit(Collider other)
    {
        var p = other.GetComponentInParent<SimplePlayerController>();
        if(p != null) p.SetOnLadder(false);
    }
}