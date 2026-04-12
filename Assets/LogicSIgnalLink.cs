using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public enum Axis { X, Y, Z }
public enum StepMode
{
    // Move along the chosen axis until we reach the target's coordinate on that axis.
    ToTarget,

    // Move along the chosen axis by a relative amount (+/-).
    Relative
}

[System.Serializable]
public struct AxisStep
{
    public Axis axis;
    public StepMode mode;

    [Tooltip("Used only when mode = Relative (units in world space). Can be negative.")]
    public float relativeDelta;

    [Tooltip("Optional pause after reaching this step point.")]
    public float pauseAfter;
}

public class LogicSIgnalLink : MonoBehaviour, ILogicNode
{
    [Header("Endpoints")]
    [Tooltip("Where the pulse begins. If null, uses this transform.")]
    public Transform start;

    [Tooltip("Assign any component that implements ILogicNode (e.g. a LogicBlock).")]
    public MonoBehaviour targetNodeBehaviour; // must implement ILogicNode
    private ILogicNode targetNode;

    [Tooltip("Used for the target position. If null, uses targetNodeBehaviour.transform.")]
    public Transform targetTransform;

    [Header("Vertical Alignment")]
    [Tooltip("Y offset applied to both start and end positions (cube centre = 0.5 for 1x1x1 cubes).")]
    public float verticalOffset = 0.5f;

    [Header("Path (no Transforms, just data)")]
    [Tooltip("These steps are executed in order. Each step moves ONLY along ONE axis.")]
    public List<AxisStep> steps = new List<AxisStep>()
    {
        new AxisStep { axis = Axis.X, mode = StepMode.ToTarget, relativeDelta = 0f, pauseAfter = 0f },
        new AxisStep { axis = Axis.Z, mode = StepMode.ToTarget, relativeDelta = 0f, pauseAfter = 0f },
    };
    [Header("Auto Path")]
    public bool autoGenerateSteps = true;

    [Header("Randomisation")]
    public bool randomiseEachPulse = true;

    [Tooltip("If true, shuffles the axis order used for auto steps every pulse.")]
    public bool randomiseAxisOrder = true;

    [Tooltip("If true, extra bends will randomise left/right each pulse.")]
    public bool randomiseBendDirection = true;


    public bool includeY = false; // handy if most links are flat
    public Axis[] autoAxisOrder = new Axis[] { Axis.X, Axis.Z, Axis.Y };

    [Tooltip("If > 0, injects extra bends for style (still axis-only). 0 = direct.")]
    public int extraBends = 0;

    [Tooltip("World units for each extra bend offset.")]
    public float bendOffset = 1f;

    [Header("Pulse Visual")]
    [Tooltip("Prefab with a TrailRenderer (and optionally a small Mesh). If null, one is created at runtime.")]
    public GameObject pulsePrefab;

    [Header("Motion")]
    public float speed = 6f;
    public float arriveEpsilon = 0.01f;
    public bool faceMoveDirection = true;
    public bool destroyPulseOnArrival = true;

    [Header("Concurrency")]
    public bool cancelPreviousOnNewSignal = true;

    private Coroutine activeRoutine;

    void Awake()
    {
        targetNode = targetNodeBehaviour as ILogicNode;
        if(start == null) start = transform;

        if(targetTransform == null && targetNodeBehaviour != null)
            targetTransform = targetNodeBehaviour.transform;
    }

    public void SetState(LogicState state)
    {
        Debug.Log("LOGIC SIGNAL LINK HAPPENING");
        if(targetNode == null || targetTransform == null)
        {
            Debug.LogWarning($"{name}: LogicSignalLink missing targetNodeBehaviour/targetTransform.");
            return;
        }

        if(cancelPreviousOnNewSignal && activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(SendPulseRoutine(state));
    }

    public LogicState GetState()
    {
        // This link itself doesn't have a meaningful state; it just forwards.
        // Return Off by default.
        return LogicState.Off;
    }

    private IEnumerator SendPulseRoutine(LogicState stateToSend)
    {
        Vector3 startPos = start.position;
        startPos.y += verticalOffset;

        Vector3 targetPos = targetTransform.position;
        targetPos.y += verticalOffset;

        GameObject pulse = SpawnPulse(startPos);

        // Build waypoint list dynamically (from steps data)
        var stepList = autoGenerateSteps
        ? GenerateSteps(startPos, targetPos, randomiseEachPulse && randomiseAxisOrder, randomiseEachPulse && randomiseBendDirection)
        : steps;

        List<Vector3> waypoints = BuildWaypoints(startPos, targetPos, stepList);


        // Always ensure final point is exactly target (safety)
        if(waypoints.Count == 0 || (waypoints[waypoints.Count - 1] - targetPos).sqrMagnitude > 0.0001f)
            waypoints.Add(targetPos);

        // Move pulse through waypoints
        for(int i = 0; i < waypoints.Count; i++)
        {
            yield return MoveToPointAxisSafe(pulse.transform, waypoints[i]);

            float pause = (i < stepList.Count) ? Mathf.Max(0f, stepList[i].pauseAfter) : 0f;
            if(pause > 0f) yield return new WaitForSeconds(pause);
        }

        // Trigger destination
        targetNode.SetState(stateToSend);

        if(destroyPulseOnArrival && pulse != null)
            Destroy(pulse);

        activeRoutine = null;
    }
    private List<AxisStep> GenerateSteps(Vector3 startPos, Vector3 targetPos, bool shuffleAxes, bool randomBendDir)
    {
        var list = new List<AxisStep>();

        // Copy axis order so we can shuffle without mutating inspector array
        Axis[] order = autoAxisOrder;
        if(shuffleAxes && order != null && order.Length > 1)
        {
            order = (Axis[])autoAxisOrder.Clone();
            // Fisher-Yates shuffle
            for(int i = 0; i < order.Length - 1; i++)
            {
                int j = Random.Range(i, order.Length);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }

        // Build the main direct steps in chosen order (axis-only to target)
        for(int i = 0; i < order.Length; i++)
        {
            Axis a = order[i];
            if(!includeY && a == Axis.Y) continue;

            float from = GetAxis(startPos, a);
            float to = GetAxis(targetPos, a);
            if(Mathf.Abs(to - from) <= arriveEpsilon) continue;

            list.Add(new AxisStep { axis = a, mode = StepMode.ToTarget, relativeDelta = 0f, pauseAfter = 0f });
        }

        // Optional: inject extra bends (detours)
        if(extraBends > 0)
        {
            Axis a1 = Axis.X;
            Axis a2 = Axis.Z;

            int insertIndex = Mathf.Min(1, list.Count);

            for(int b = 0; b < extraBends; b++)
            {
                float dir = (b % 2 == 0) ? 1f : -1f;
                if(randomBendDir)
                    dir = (Random.value < 0.5f) ? 1f : -1f;

                list.Insert(insertIndex++, new AxisStep
                {
                    axis = a1,
                    mode = StepMode.Relative,
                    relativeDelta = bendOffset * dir,
                    pauseAfter = 0f
                });

                list.Insert(insertIndex++, new AxisStep
                {
                    axis = a2,
                    mode = StepMode.Relative,
                    relativeDelta = bendOffset * -dir,
                    pauseAfter = 0f
                });

                list.Insert(insertIndex++, new AxisStep { axis = a1, mode = StepMode.ToTarget });
                list.Insert(insertIndex++, new AxisStep { axis = a2, mode = StepMode.ToTarget });
            }
        }

        return list;
    }
    private List<Vector3> BuildWaypoints(Vector3 startPos, Vector3 targetPos, List<AxisStep> stepList)
    {
        var pts = new List<Vector3>();
        Vector3 cur = startPos;
        cur.y = startPos.y;

        for(int i = 0; i < stepList.Count; i++)
        {
            AxisStep s = stepList[i];
            Vector3 next = cur;

            float targetAxisValue = GetAxis(targetPos, s.axis);

            if(s.mode == StepMode.ToTarget)
            {
                SetAxis(ref next, s.axis, targetAxisValue);
            }
            else // Relative
            {
                float v = GetAxis(cur, s.axis) + s.relativeDelta;
                SetAxis(ref next, s.axis, v);
            }

            next.y = startPos.y;
            // Only add if it actually changes position (prevents zero-length steps)
            if((next - cur).sqrMagnitude > 0.0000001f)
                pts.Add(next);

            cur = next;
        }

        return pts;
    }

    private IEnumerator MoveToPointAxisSafe(Transform mover, Vector3 destination)
    {
        // This ensures we *only* move along the axis that differs (one axis at a time).
        // If destination differs in multiple axes (shouldn't, if steps are valid), we still handle it:
        // move X then Y then Z to reach it.
        Vector3 cur = mover.position;

        // Move per-axis in a deterministic order
        yield return MoveAlongSingleAxis(mover, Axis.X, destination);
        yield return MoveAlongSingleAxis(mover, Axis.Y, destination);
        yield return MoveAlongSingleAxis(mover, Axis.Z, destination);
    }

    private IEnumerator MoveAlongSingleAxis(Transform mover, Axis axis, Vector3 destination)
    {
        Vector3 pos = mover.position;
        float from = GetAxis(pos, axis);
        float to = GetAxis(destination, axis);

        if(Mathf.Abs(to - from) <= arriveEpsilon)
            yield break;

        // Move only this axis
        while(true)
        {
            pos = mover.position;

            float curAxis = GetAxis(pos, axis);
            float newAxis = Mathf.MoveTowards(curAxis, to, speed * Time.deltaTime);

            Vector3 next = pos;
            SetAxis(ref next, axis, newAxis);

            Vector3 delta = next - pos;
            if(faceMoveDirection && delta.sqrMagnitude > 0.0000001f)
                mover.forward = delta.normalized;

            mover.position = next;

            if(Mathf.Abs(to - newAxis) <= arriveEpsilon)
            {
                // Snap cleanly
                Vector3 snap = mover.position;
                SetAxis(ref snap, axis, to);
                mover.position = snap;
                yield break;
            }

            yield return null;
        }
    }

    private GameObject SpawnPulse(Vector3 pos)
    {
        if(pulsePrefab != null)
            return Instantiate(pulsePrefab, pos, Quaternion.identity);

        // Runtime fallback: tiny sphere + TrailRenderer
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "LogicPulse (Runtime)";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.15f;

        // Optional: remove collider
        var col = go.GetComponent<Collider>();
        if(col) Destroy(col);

        var tr = go.AddComponent<TrailRenderer>();
        tr.time = 0.25f;
        tr.minVertexDistance = 0.05f;
        tr.widthMultiplier = 0.08f;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;

        return go;
    }

    private static float GetAxis(Vector3 v, Axis a)
    {
        switch(a)
        {
            case Axis.X: return v.x;
            case Axis.Y: return v.y;
            default: return v.z;
        }
    }

    private static void SetAxis(ref Vector3 v, Axis a, float value)
    {
        switch(a)
        {
            case Axis.X: v.x = value; break;
            case Axis.Y: v.y = value; break;
            case Axis.Z: v.z = value; break;
        }
    }
    // -------------------- GIZMOS --------------------
#if UNITY_EDITOR
    [Header("Editor Gizmos")]
    public bool drawGizmos = true;
    public bool drawOnlyWhenSelected = true;
    public Color gizmoColor = new Color(0.2f, 1f, 0.2f, 1f);
    public float lineThickness = 15f;

    private void OnDrawGizmos()
    {
        if(!drawGizmos || drawOnlyWhenSelected) return;
        DrawLinkGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if(!drawGizmos) return;
        DrawLinkGizmos();
    }

    private void DrawLinkGizmos()
    {
        Transform s = start != null ? start : transform;

        Transform t = targetTransform != null
            ? targetTransform
            : (targetNodeBehaviour != null ? targetNodeBehaviour.transform : null);

        if(s == null || t == null) return;

        Vector3 startPos = s.position + Vector3.up * verticalOffset;
        Vector3 targetPos = t.position + Vector3.up * verticalOffset;

        List<AxisStep> stepList = autoGenerateSteps
            ? GenerateSteps(startPos, targetPos, false, false)
            : steps;

        List<Vector3> waypoints = BuildWaypoints(startPos, targetPos, stepList);

        if(waypoints.Count == 0 ||
            (waypoints[waypoints.Count - 1] - targetPos).sqrMagnitude > 0.0001f)
            waypoints.Add(targetPos);

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always; // Draw over everything
        Handles.color = gizmoColor;

        Vector3 prev = startPos;

        for(int i = 0; i < waypoints.Count; i++)
        {
            Vector3 p = waypoints[i];
            Handles.DrawAAPolyLine(lineThickness, prev, p);
            prev = p;
        }

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual; // Restore default
    }
#endif
    // -------------------- END GIZMOS --------------------

}