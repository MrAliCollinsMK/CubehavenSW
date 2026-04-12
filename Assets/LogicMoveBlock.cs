using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogicMoveBlock : LogicBlock
{
    [Header("Movement")]
    public Vector3 moveOffset = Vector3.up;
    public float moveDuration = 0.5f;
    public bool useLocalSpace = true;
    public bool smooth = true;

    [Header("Axis-Ordered Movement")]
    public bool moveAxisThenY = false;   // if true: move along one axis first, then Y (default final axis)
    public Axis firstAxis = Axis.X;      // which axis to do first (X or Z typically)
    public float firstAxisTimeRatio = 0.5f; // portion of duration spent on phase 1 (0..1)

    public enum Axis { X, Y, Z }

    [Header("Material Change (Moving + Colliding)")]
    [Tooltip("Material to use ONLY when this block is moving AND colliding with another cube.")]
    public Material movingCollisionMaterial;

    [Tooltip("If assigned, only collisions with objects on these layers count as 'another cube'.")]
    public LayerMask cubeCollisionLayers = ~0; // default: everything

    [Tooltip("If true, ignore trigger colliders.")]
    public bool ignoreTriggers = true;

    [Tooltip("Movement epsilon used to decide if we're 'moving'.")]
    public float movingEpsilon = 0.0001f;

    Vector3 startPos;
    Vector3 targetPos;

    Coroutine moveRoutine;

    // --- material state ---
    Renderer cachedRenderer;
    Material originalMaterial;
    bool isMoving;
    Vector3 lastPos;

    // Track active collisions (so we don’t get fooled by multiple colliders)
    readonly HashSet<Collider> activeCubeCollisions = new HashSet<Collider>();

    public AudioSource msource;

    void Start()
    {
        startPos = useLocalSpace ? transform.localPosition : transform.position;
        targetPos = startPos + moveOffset;

        cachedRenderer = GetComponentInChildren<Renderer>();
        if(cachedRenderer != null)
            originalMaterial = cachedRenderer.sharedMaterial;

        lastPos = useLocalSpace ? transform.localPosition : transform.position;
        msource = GetComponent<AudioSource>();
        ApplyState();
        UpdateMaterialState(); // ensure correct initial state
    }

    void Update()
    {
        // Extra safety: if something else moves us (or physics nudges), still works.
        Vector3 current = useLocalSpace ? transform.localPosition : transform.position;
        isMoving = (current - lastPos).sqrMagnitude > (movingEpsilon * movingEpsilon);
        lastPos = current;

        UpdateMaterialState();
    }

    protected override void ApplyState()
    {
        base.ApplyState();

       Vector3 desired = (currentState == LogicState.On) ? targetPos : startPos;

        if(moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MoveTo(desired));
    }

    IEnumerator MoveTo(Vector3 dest)
    {

        Vector3 from = useLocalSpace ? transform.localPosition : transform.position;

        // If we’re doing an instant snap, treat that as "not moving" immediately after.
        if(!smooth || moveDuration <= 0f)
        {
            SetPosition(dest);
            yield break;
        }
        if(msource == null)
        {
            if(msource.clip != null)
            {
                msource.Play();
            }


        }
        if(!moveAxisThenY)
        {
            yield return LerpPosition(from, dest, moveDuration);
            yield break;
        }

        // ---- Axis-ordered move: phase 1 (firstAxis), then phase 2 (Y by default) ----
        float ratio = Mathf.Clamp01(firstAxisTimeRatio);
        float t1 = moveDuration * ratio;
        float t2 = moveDuration - t1;

        Axis a1 = firstAxis;
        Axis a2 = Axis.Y;

        if(a1 == Axis.Y) a1 = Axis.X;

        Vector3 mid = from;

        mid = SetAxis(mid, a1, GetAxis(dest, a1));

        Vector3 end = dest;

        mid = SetAxis(mid, Axis.X, GetAxis(dest, Axis.X));
        mid = SetAxis(mid, Axis.Z, GetAxis(dest, Axis.Z));
        mid = SetAxis(mid, a1, GetAxis(dest, a1));
        mid = SetAxis(mid, Axis.Y, GetAxis(from, Axis.Y));

        if(t1 > 0f)
            yield return LerpPosition(from, mid, t1);
        else
            SetPosition(mid);

        Vector3 yEnd = mid;
        yEnd = SetAxis(yEnd, Axis.Y, GetAxis(dest, Axis.Y));

        if(t2 > 0f)
            yield return LerpPosition(mid, yEnd, t2);
        else
            SetPosition(yEnd);

        SetPosition(dest);
    }

    IEnumerator LerpPosition(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while(t < 1f)
        {
            t += Time.deltaTime / duration;
            SetPosition(Vector3.Lerp(from, to, t));
            yield return null;
        }
        SetPosition(to);
    }

    static float GetAxis(Vector3 v, Axis a)
    {
        return a switch
        {
            Axis.X => v.x,
            Axis.Y => v.y,
            Axis.Z => v.z,
            _ => v.y
        };
    }

    static Vector3 SetAxis(Vector3 v, Axis a, float value)
    {
        switch(a)
        {
            case Axis.X: v.x = value; break;
            case Axis.Y: v.y = value; break;
            case Axis.Z: v.z = value; break;
        }
        return v;
    }

    void SetPosition(Vector3 p)
    {
        if(useLocalSpace)
            transform.localPosition = p;
        else
            transform.position = p;
    }

    // -------------------------
    // Collision tracking
    // -------------------------

    void OnCollisionEnter(Collision collision)
    {
        TryAddCollision(collision.collider);
        UpdateMaterialState();
    }

    void OnCollisionStay(Collision collision)
    {
        TryAddCollision(collision.collider);
        UpdateMaterialState();
    }

    void OnCollisionExit(Collision collision)
    {
        if(collision.collider != null)
            activeCubeCollisions.Remove(collision.collider);

        UpdateMaterialState();
    }

    void TryAddCollision(Collider other)
    {
        if(other == null) return;
        if(ignoreTriggers && other.isTrigger) return;

        // layer filter (treat this as "is it another cube?")
        int otherLayerMaskBit = 1 << other.gameObject.layer;
        if((cubeCollisionLayers.value & otherLayerMaskBit) == 0)
            return;

        // don’t count self / children
        if(other.transform == transform || other.transform.IsChildOf(transform))
            return;

        activeCubeCollisions.Add(other);
    }

    bool IsCollidingWithCube()
    {
        // Clean out any destroyed colliders
        activeCubeCollisions.RemoveWhere(c => c == null);
        return activeCubeCollisions.Count > 0;
    }

    void UpdateMaterialState()
    {
        if(cachedRenderer == null) return;
        if(originalMaterial == null) return;

        bool shouldHighlight = isMoving && IsCollidingWithCube() && movingCollisionMaterial != null;

        // Use sharedMaterial so we don't instantiate per-object materials at runtime.
        cachedRenderer.sharedMaterial = shouldHighlight ? movingCollisionMaterial : originalMaterial;
    }
}