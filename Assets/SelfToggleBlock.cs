using UnityEngine;

public class SelfToggleBlock : LogicBlock, IInteractable
{
    public void Interact(SimplePlayerController player)
    {
        
        LogicState next =
            currentState == LogicState.On
                ? LogicState.Off
                : LogicState.On;

        SetState(next);
    }
}