using UnityEngine;

public class LogicSwitch : MonoBehaviour, IInteractable
{
    [Header("Output")]
    public LogicState outputState = LogicState.On;

    [Tooltip("If true, pressing again will toggle On/Off")]
    public bool toggleOnRepeat = false;

    [Header("Targets")]
    public LogicBlock[] targets;

    [Header("Behaviour")]
    public bool oneShot = true;

    bool used;
    bool toggledState; // tracks current toggle state

    public void Interact(SimplePlayerController player)
    {
        if(oneShot && used) return;

        LogicState stateToApply = outputState;

        if(toggleOnRepeat)
        {
            toggledState = !toggledState;
            stateToApply = toggledState ? LogicState.On : LogicState.Off;
        }

        foreach(var t in targets)
        {
            if(t != null)
                t.SetState(stateToApply);
        }

        used = true;
    }
}
