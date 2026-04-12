using System.Collections;
using UnityEngine;

public class AutoMoveBlock : MonoBehaviour
{
    [Header("Path")]
    public Vector3 moveOffset = new Vector3(0f, 0f, 3f); // how far from start to end
    public bool useLocalSpace = true;

    [Header("Timing")]
    public float moveDuration = 2.0f;     // time to go from start -> end
    public float waitAtEnds = 1f;       // pause at start and end
    public bool smoothStep = true;        // nicer easing than linear

    [Header("Behaviour")]
    public bool startMovingOnAwake = true;

    Vector3 startPos;
    Vector3 endPos;
    Coroutine routine;

    void Awake()
    {
        startPos = useLocalSpace ? transform.localPosition : transform.position;
        endPos = startPos + moveOffset;

        if(startMovingOnAwake)
            StartMoving();
    }

    public void StartMoving()
    {
        if(routine != null) StopCoroutine(routine);
        routine = StartCoroutine(MoveLoop());
    }

    public void StopMoving()
    {
        if(routine != null) StopCoroutine(routine);
        routine = null;
    }

    IEnumerator MoveLoop()
    {
        // Always begin from wherever we currently are (nice for scene edits)
        Vector3 a = useLocalSpace ? transform.localPosition : transform.position;
        Vector3 b = endPos;

        while(true)
        {
            yield return MoveTo(b);
            if(waitAtEnds > 0f) yield return new WaitForSeconds(waitAtEnds);

            // swap targets (ping-pong)
            var temp = a; a = b; b = temp;
        }
    }

    IEnumerator MoveTo(Vector3 dest)
    {
        Vector3 from = useLocalSpace ? transform.localPosition : transform.position;

        if(moveDuration <= 0f)
        {
            SetPos(dest);
            yield break;
        }

        float t = 0f;
        while(t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float u = Mathf.Clamp01(t);

            if(smoothStep)
                u = u * u * (3f - 2f * u); // SmoothStep curve

            SetPos(Vector3.LerpUnclamped(from, dest, u));
            yield return null;
        }

        SetPos(dest);
    }

    void SetPos(Vector3 p)
    {
        if(useLocalSpace) transform.localPosition = p;
        else transform.position = p;
    }
}