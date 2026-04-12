using System.Collections;
using UnityEngine;

public class TimedLogicSwitch : MonoBehaviour, IInteractable
{
    [Header("Output")]
    public LogicState outputState = LogicState.On;
    public LogicState revertState = LogicState.Off;

    [Header("Timing")]
    public float duration = 3.0f;
    public bool restartTimerOnRepress = true;

    [Header("Targets")]
    public LogicBlock[] targets;

    [Header("Behaviour")]
    public bool oneShot = false;          // usually false for timed switches
    public bool disableWhileActive = true; // prevents spam while timer running

    bool used;
    bool isActive;
    Coroutine timerRoutine;

    public void Interact(SimplePlayerController player)
    {
        if(oneShot && used) return;
        used = true;

        if(disableWhileActive && isActive)
        {
            if(restartTimerOnRepress && timerRoutine != null)
            {
                StopCoroutine(timerRoutine);
                timerRoutine = StartCoroutine(TimerRoutine());
            }
            return;
        }

        // Apply ON (or whatever outputState is)
        ApplyToTargets(outputState);

        // Start / restart timer
        if(timerRoutine != null) StopCoroutine(timerRoutine);
        timerRoutine = StartCoroutine(TimerRoutine());
    }

    IEnumerator TimerRoutine()
    {
        isActive = true;

        // Safety clamp
        float t = Mathf.Max(0f, duration);
        if(t > 0f) yield return new WaitForSeconds(t);

        // Revert
        ApplyToTargets(revertState);

        isActive = false;
        timerRoutine = null;
    }

    void ApplyToTargets(LogicState state)
    {
        if(targets == null) return;
        for(int i = 0; i < targets.Length; i++)
        {
            if(targets[i] != null)
                targets[i].SetState(state);
        }
    }
}
