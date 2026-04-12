using System.Collections.Generic;
using UnityEngine;

public class PlatformPassengerCarrier : MonoBehaviour
{
    [Header("Who gets carried")]
    public LayerMask passengerMask;                 // set to Player layer
    public bool onlyCarryWhenGrounded = true;       // avoids "sticky" mid-air carry
    public float groundedGrace = 0.05f;             // helps when CC briefly ungrounds on edges

    Vector3 lastPos;
    readonly List<SimplePlayerController> passengers = new();

    void Awake()
    {
        lastPos = transform.position;
    }

    void LateUpdate()
    {
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;

        if(delta.sqrMagnitude < 0.0000001f) return;

        for(int i = passengers.Count - 1; i >= 0; i--)
        {
            var p = passengers[i];
            if(p == null) { passengers.RemoveAt(i); continue; }

            if(onlyCarryWhenGrounded)
            {
                // "Grounded" can flicker on edges; grace helps.
                bool groundedish = p.IsGrounded || (p.VerticalVelocity <= groundedGrace);
                if(!groundedish) continue;
            }

            // Move the character by the platform's movement this frame
            var cc = p.GetComponent<CharacterController>();
            if(cc != null)
                cc.Move(delta);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if(((1 << other.gameObject.layer) & passengerMask) == 0) return;

        var p = other.GetComponentInParent<SimplePlayerController>();
        if(p != null && !passengers.Contains(p))
            passengers.Add(p);
    }

    void OnTriggerExit(Collider other)
    {
        if(((1 << other.gameObject.layer) & passengerMask) == 0) return;

        var p = other.GetComponentInParent<SimplePlayerController>();
        if(p != null)
            passengers.Remove(p);
    }
}
