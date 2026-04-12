using UnityEngine;

public class LogicORGate : LogicBlock
{
    [Header("Inputs")]
    [Tooltip("If ANY of these are ON, the gate outputs ON.")]
    public LogicBlock[] inputs;
    // either modify this so it accepts 2 IInteractables, or just make 2 gates
    
    [Tooltip("If true, zero inputs = OFF. If false, zero inputs = ON.")]
    public bool requireAtLeastOneInput = true;

    [Header("Polling")]
    public float pollInterval = 1f;

    void OnEnable()
    {
        InvokeRepeating(nameof(Recalculate), 0f, pollInterval);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(Recalculate));
    }

    void Recalculate()
    {
        base.SetState(ComputeDesiredState());
    }

    LogicState ComputeDesiredState()
    {
        if(inputs == null || inputs.Length == 0)
            return requireAtLeastOneInput ? LogicState.Off : LogicState.On;

        for(int i = 0; i < inputs.Length; i++)
        {
            if(inputs[i] != null && inputs[i].GetState() == LogicState.On)
                return LogicState.On;
        }

        return LogicState.Off;
    }
}
