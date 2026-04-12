using UnityEngine;

public class LogicANDGate : LogicBlock
{
    [Header("Inputs")]
    [Tooltip("All of these must be ON for this gate to output ON.")]
    public LogicBlock[] inputs;

    [Tooltip("If true, zero inputs = OFF. If false, zero inputs = ON.")]
    public bool requireAtLeastOneInput = true;

    [Header("Polling")]
    [Tooltip("How often (in seconds) to re-evaluate inputs.")]
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
        LogicState desired = ComputeDesiredState();

        // IMPORTANT:
        // Call base.SetState so LogicBlock handles visuals + forwarding
        base.SetState(desired);
    }

    LogicState ComputeDesiredState()
    {
        if(inputs == null || inputs.Length == 0)
            return requireAtLeastOneInput ? LogicState.Off : LogicState.On;

        for(int i = 0; i < inputs.Length; i++)
        {
            if(inputs[i] == null || inputs[i].GetState() != LogicState.On)
                return LogicState.Off;
        }

        return LogicState.On;
    }
}
