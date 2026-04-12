using UnityEngine;

public class LogicBlock : MonoBehaviour, ILogicNode
{
    public LogicState currentState = LogicState.Off;

    [Header("Visuals")]
    public GameObject onObject;
    public GameObject offObject;

    [Header("Optional: forward to next node")]
    public MonoBehaviour nextNode; // assign something that implements ILogicNode
    ILogicNode next;

    void Awake()
    {
        next = nextNode as ILogicNode;
    }

    public virtual void SetState(LogicState state)
    {
        if(currentState == state) return;

        currentState = state;
        ApplyState();

        if(next != null)
            next.SetState(state);
    }

    // ✅ NEW: for “press should toggle regardless of who pressed it”
    public virtual void ToggleState()
    {
        SetState(currentState == LogicState.On ? LogicState.Off : LogicState.On);
    }

    // ✅ NEW: for “retrigger even if same state”
    public virtual void ForceApply()
    {
        ApplyState();

        // Optional: forward force-apply as a *state resend*
        if(next != null)
            next.SetState(currentState);
    }

    public LogicState GetState() => currentState;

    protected virtual void ApplyState()
    {
        if(onObject) onObject.SetActive(currentState == LogicState.On);
        if(offObject) offObject.SetActive(currentState == LogicState.Off);
    }
}