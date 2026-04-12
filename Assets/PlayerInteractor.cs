using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Refs")]
    public SimplePlayerController player; // drag in or auto-find

    [Header("Interaction")]
    public float interactRange = 1.5f;
    public LayerMask interactMask;

    [Tooltip("Thickness of the interaction ray (capsule radius).")]
    public float interactThickness = 0.25f;

    [Tooltip("Optional: camera or model pivot. If null, uses player.transform.")]
    public Transform facingSource;

    [Tooltip("Height offset for the cast origin (world space).")]
    public float originHeight = 0.6f;

    [Tooltip("How tall the capsule is (in world units). Small = more ray-like.")]
    public float capsuleHeight = 0.2f;

    [Header("Timing")]
    public float impactDelay = 0.3f; // 0 = instant
    public bool playAnimOnlyOnHit = true;

    [Header("Debug")]
    public bool debugDraw = true;

    float timer;
    IInteractable[] pending;
    bool pendingQueued;

    void Awake()
    {
        if(player == null) player = GetComponent<SimplePlayerController>();
        if(facingSource == null && player != null) facingSource = player.transform;
    }

    void Update()
    {
        if(pendingQueued)
        {
            timer -= Time.deltaTime;
            if(timer <= 0f)
                FirePending();
        }
    }

    public void TryInteract()
    {
        if(player == null) return;
        if(pendingQueued) return;

        Transform face = (facingSource != null) ? facingSource : player.transform;

        Vector3 origin = player.transform.position + Vector3.up * originHeight;
        Vector3 dir = face.forward;
        dir.y = 0f; // optional: keeps interaction on ground plane (good for cube puzzles)
        if(dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        // Small upright capsule at origin (more stable than using full-range endpoints)
        float half = Mathf.Max(0.01f, capsuleHeight * 0.5f);
        Vector3 p1 = origin + Vector3.up * half;
        Vector3 p2 = origin - Vector3.up * half;

#if UNITY_EDITOR
        if(debugDraw)
        {
            // draw the swept path as a capsule at start and end + connecting lines
            DebugDrawCapsule(p1, p2, interactThickness, Color.cyan, 0f);
            DebugDrawCapsule(p1 + dir * interactRange, p2 + dir * interactRange, interactThickness, Color.red, 10f);
            Debug.DrawLine(p1, p1 + dir * interactRange, Color.cyan,10f);
            Debug.DrawLine(p2, p2 + dir * interactRange, Color.cyan,10f);
        }
#endif
        if(!playAnimOnlyOnHit)
            player.PlayInteractAnim();
        // “Fat ray” cast
        if(!Physics.CapsuleCast(
            p1,
            p2,
            interactThickness,
            dir,
            out RaycastHit hit,
            interactRange,
            interactMask,
            QueryTriggerInteraction.Collide))
        {
            return;
        }

        // Pull interactables from hit hierarchy
        var raw = hit.collider.GetComponentsInParent<IInteractable>(includeInactive: false);
        if(raw == null || raw.Length == 0)
            return;

        // De-dupe
        var seen = new HashSet<IInteractable>();
        var list = new List<IInteractable>(raw.Length);
        for(int i = 0; i < raw.Length; i++)
        {
            var ia = raw[i];
            if(ia == null) continue;
            if(!seen.Add(ia)) continue;
            list.Add(ia);
        }
        if(list.Count == 0) return;

       

        if(playAnimOnlyOnHit)
            player.PlayInteractAnim();

        if(impactDelay <= 0f)
        {
            for(int i = 0; i < list.Count; i++)
                list[i].Interact(player);
        }
        else
        {
            pending = list.ToArray();
            timer = impactDelay;
            pendingQueued = true;
        }
    }

    void FirePending()
    {
        if(pending != null)
        {
            for(int i = 0; i < pending.Length; i++)
                pending[i]?.Interact(player);
        }
        pending = null;
        pendingQueued = false;
    }

    // ---------- Debug helpers ----------

    void DebugDrawCapsule(Vector3 p1, Vector3 p2, float radius, Color color, float duration = 10f, int segments = 16)
    {
        DebugDrawSphere(p1, radius, color, duration, segments);
        DebugDrawSphere(p2, radius, color, duration, segments);

        // Connect a few cardinal directions so you can “see” the thickness
        Vector3 up = Vector3.up * radius;
        Vector3 right = Vector3.right * radius;
        Vector3 forward = Vector3.forward * radius;

        Debug.DrawLine(p1 + up, p2 + up, color, duration);
        Debug.DrawLine(p1 - up, p2 - up, color, duration);

        Debug.DrawLine(p1 + right, p2 + right, color, duration);
        Debug.DrawLine(p1 - right, p2 - right, color, duration);

        Debug.DrawLine(p1 + forward, p2 + forward, color, duration);
        Debug.DrawLine(p1 - forward, p2 - forward, color, duration);
    }

    void DebugDrawSphere(Vector3 center, float radius, Color color, float duration = 0f, int segments = 16)
    {
        float angleStep = 360f / segments;

        for(int i = 0; i < segments; i++)
        {
            float a0 = angleStep * i * Mathf.Deg2Rad;
            float a1 = angleStep * (i + 1) * Mathf.Deg2Rad;

            // XY
            Debug.DrawLine(
                center + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * radius,
                center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * radius,
                color, duration
            );

            // XZ
            Debug.DrawLine(
                center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius,
                center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius,
                color, duration
            );

            // YZ
            Debug.DrawLine(
                center + new Vector3(0, Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
                center + new Vector3(0, Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
                color, duration
            );
        }
    }
}
