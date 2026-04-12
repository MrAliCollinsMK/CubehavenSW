using UnityEngine;

public class LogicDisableBlock : LogicBlock
{
    [Header("Disable Targets")]
    public GameObject[] targetsToDisable;

    [Tooltip("If true, disables on LogicState.On. If false, disables on LogicState.Off.")]
    public bool disableWhenOn = true;

    protected override void ApplyState()
    {
        base.ApplyState();

        bool shouldDisable = disableWhenOn
            ? (currentState == LogicState.On)
            : (currentState == LogicState.Off);

        if(!shouldDisable) return;

        if(targetsToDisable == null) return;

        for(int i = 0; i < targetsToDisable.Length; i++)
        {
            var go = targetsToDisable[i];
            if(go != null)
                go.SetActive(false);
        }
    }
}