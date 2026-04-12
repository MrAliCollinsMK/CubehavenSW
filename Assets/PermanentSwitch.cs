using UnityEngine;

public class PermanentSwitch : MonoBehaviour
{
    [Header("Targets")]
    public LogicBlock[] targets;

    [Header("Output")]
    public LogicState outputState = LogicState.On;

    [Header("Animation")]
    public Animation anim;
    public string pressAnimName = "Press";

    [Header("State")]
    public bool triggered = false;

    public AudioSource msource;

    void Awake()
    {
        if(anim == null)
            anim = GetComponentInChildren<Animation>();
        msource=GetComponent<AudioSource>();
     
    }

    private void OnTriggerEnter(Collider other)
    {
        if(triggered) return;

        var player = other.GetComponentInParent<SimplePlayerController>();
        if(player == null) return;

        Trigger();
    }

    void Trigger()
    {
        triggered = true;

        // Play press animation
        if(anim != null && anim.GetClip(pressAnimName) != null)
            anim.Play(pressAnimName);

        if (msource!= null)
        {
            msource.Play();
        }
        // Apply logic state
        if(targets != null)
        {
            for(int i = 0; i < targets.Length; i++)
            {
                if(targets[i] != null)
                    targets[i].SetState(outputState);
            }
        }

        // Disable this trigger so it never fires again
        var triggerCol = GetComponentInChildren<Collider>();
        if(triggerCol != null)
            triggerCol.enabled = false;
    }
}