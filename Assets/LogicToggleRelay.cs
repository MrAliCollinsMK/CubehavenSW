using UnityEditor;
using UnityEngine;

public class LogicToggleRelay : LogicBlock
{
    public LogicBlock[] outputs;
    LogicState lastState = LogicState.Off;

    protected override void ApplyState()
    {
        base.ApplyState();

        // Fire only on rising edge (Off -> On)
        if(lastState == LogicState.Off && currentState == LogicState.On)
        {
            foreach(var o in outputs)
            {
                if(o == null) continue;
                o.ToggleState();
                Debug.Log($"Relay TOGGLE {o.name}");
            }
        }

        lastState = currentState;
    }
#if UNITY_EDITOR
    [Header("Editor Gizmos")]
    public bool drawGizmos = true;
    public float lineThickness = 12f;

    private void OnDrawGizmos()
    {
        if(!drawGizmos || outputs == null) return;

        // Red when OFF, green when ON
        Color c = currentState == LogicState.On
            ? new Color(0.2f, 1f, 0.2f, 1f)
            : new Color(1f, 0.2f, 0.2f, 1f);

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = c;

        Vector3 start = transform.position + Vector3.up * 0.5f;

        foreach(var o in outputs)
        {
            if(o == null) continue;

            Vector3 end = o.transform.position + Vector3.up * 0.5f;
            Handles.DrawAAPolyLine(lineThickness, start, end);
        }

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
    }
#endif
}