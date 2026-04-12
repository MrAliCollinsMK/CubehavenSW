using UnityEngine;

public class LogicPressurePlate : MonoBehaviour
{
    [Header("Targets")]
    public LogicBlock[] targets;

    [Header("Who can trigger")]
    public LayerMask triggerMask;

    [Header("Optional Animation")]
    public Animation anim;
    public string holdAnimName = "Hold";

    int triggerCount = 0;

    public AudioSource msource;

    void Awake()
    {
        if(anim == null)
            anim = GetComponentInChildren<Animation>();
        msource=GetComponent<AudioSource>();
    }

    void OnTriggerEnter(Collider other)
    {
        if(((1 << other.gameObject.layer) & triggerMask) == 0)
            return;

        triggerCount++;

        if(triggerCount == 1)
        {
            SetTargets(LogicState.On);
            PlayHold(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if(((1 << other.gameObject.layer) & triggerMask) == 0)
            return;

        triggerCount--;

        if(triggerCount <= 0)
        {
            triggerCount = 0;
            SetTargets(LogicState.Off);
            PlayHold(false);
        }
    }

    void SetTargets(LogicState state)
    {
        foreach(var t in targets)
        {
            if(t != null)
                t.SetState(state);
        }
    }

    void PlayHold(bool down)
    {
        if(anim == null) return;
        if(anim.GetClip(holdAnimName) == null) return;

        var clip = anim[holdAnimName];

        if(down)
        {
            clip.speed = 1f;
            clip.time = 0f;
            anim.Play(holdAnimName);
        }
        else
        {
            // Reverse animation to come back up
            clip.speed = -1f;
            clip.time = clip.length;
            anim.Play(holdAnimName);
        }
        if (msource != null) { msource.Play(); }
    }
}