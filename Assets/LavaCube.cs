using UnityEngine;

public class LavaCube : MonoBehaviour
{
    [Header("Knockback")]
    public float knockbackStrength = 6f;
    public float upwardBoost = 1.5f;
    public float lockTime = 0.2f;

    [Header("Optional")]
    public bool alsoBlockMovement = false; // if true, add a non-trigger collider too

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<SimplePlayerController>();
        if(player == null) return;

        // Push BACK the way they came (opposite of last movement direction)
        Vector3 back = -player.LastMoveDir;
        back.y = 0f;
         
        // Fallback if they somehow had no movement direction (e.g. dropped straight down)
        if(back.sqrMagnitude < 0.0001f)
            back = -player.transform.forward;

        player.ApplyHazardKnockback(back.normalized, knockbackStrength, upwardBoost, lockTime);
    }
}