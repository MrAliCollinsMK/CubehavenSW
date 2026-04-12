using UnityEditor;
using UnityEngine;

public class LogicRelay : LogicBlock
{
    public LogicBlock[] outputs;

    LogicState lastState = LogicState.Off;
    void Start()
    {
        lastState = currentState;
    }
    protected override void ApplyState()
    {
        base.ApplyState();

        foreach(var o in outputs)
        {
            if(o == null) continue;

            var s = o.GetState();
            var inv = (s == LogicState.On) ? LogicState.Off : LogicState.On;
            o.SetState(inv);
        }
    }

#if UNITY_EDITOR
    [Header("Editor Gizmos")]
    public bool drawGizmos = true;
    public float lineThickness = 12f;

    private void OnDrawGizmos()
    {
        if(!drawGizmos || outputs == null) return;

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