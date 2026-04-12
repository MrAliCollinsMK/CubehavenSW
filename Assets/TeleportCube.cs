using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class TeleportCube : MonoBehaviour
{
    [Header("Teleport Target")]
    [Tooltip("Destination TeleportCube (preferred). If set, destinationTransform is ignored.")]
    public TeleportCube destinationTeleporter;

    [Tooltip("Fallback destination transform if you don't want to reference another TeleportCube.")]
    public Transform destinationTransform;

    [Header("Arrival Point")]
    [Tooltip("Optional: a child transform that marks where the player should land. If null, uses this.transform.")]
    public Transform arrivalPoint;
    
    [Tooltip("Optional: explicitly choose the exact child transform to teleport to (overrides destinationTeleporter/destinationTransform).")]
    public Transform destinationArrivalPoint;

    [Header("Arrival Offset")]
    [Tooltip("World-space vertical offset applied on arrival to avoid clipping.")]
    public float verticalOffset = 0.5f;

    [Header("Options")]
    public bool preserveVelocity = false;
    public bool snapRotation = true;
    public float reentryCooldown = 0.2f;

    [Header("One-way")]
    [Tooltip("If true: after teleporting, the destination teleporter is disabled (no return).")]
    public bool oneWay = false;

    [Tooltip("If true and oneWay is enabled, this teleporter also disables itself after use.")]
    public bool disableSelfOnUse = false;

    [Header("Visuals (Child Particles)")]
    [Tooltip("If true, all child ParticleSystems will play when active and stop when inactive.")]
    public bool controlChildParticles = true;

    [Tooltip("Also toggle all child Renderers when active/inactive (handy for glow meshes).")]
    public bool controlChildRenderers = false;

    [Header("State")]
    [SerializeField] private bool isActive = true;

    private bool coolingDown = false;

    private ParticleSystem[] cachedParticles;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;

    void Awake()
    {
        if(arrivalPoint == null)
            arrivalPoint = transform; // sensible fallback

        CacheChildren();
        ApplyActiveVisualsAndColliders();
    }

    private void CacheChildren()
    {
        cachedParticles = GetComponentsInChildren<ParticleSystem>(true);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnValidate()
    {
        // Keep visuals consistent in editor when toggling isActive/flags.
        CacheChildren();
        ApplyActiveVisualsAndColliders();
    }

    private void OnTriggerEnter(Collider other)
    {
        if(!isActive) return;
        if(coolingDown) return;

        var player = other.GetComponentInParent<SimplePlayerController>();
        if(player == null) return;

        Teleport(player);
    }

    public void SetActive(bool active)
    {
        isActive = active;
        ApplyActiveVisualsAndColliders();
    }

    private void ApplyActiveVisualsAndColliders()
    {
        // Colliders: if inactive, disable trigger colliders so it can't be used.
        if(cachedColliders != null)
        {
            for(int i = 0; i < cachedColliders.Length; i++)
            {
                if(cachedColliders[i] == null) continue;

                // Keep non-trigger colliders alone if you want the cube solid.
                // Only disable triggers (usually what you use for teleport activation).
                if(cachedColliders[i].isTrigger)
                    cachedColliders[i].enabled = isActive;
            }
        }

        if(controlChildParticles && cachedParticles != null)
        {
            for(int i = 0; i < cachedParticles.Length; i++)
            {
                var ps = cachedParticles[i];
                if(ps == null) continue;

                if(isActive)
                {
                    if(!ps.isPlaying) ps.Play(true);
                }
                else
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        if(controlChildRenderers && cachedRenderers != null)
        {
            for(int i = 0; i < cachedRenderers.Length; i++)
            {
                var r = cachedRenderers[i];
                if(r == null) continue;
                r.enabled = isActive;
            }
        }
    }

    private void Teleport(SimplePlayerController player)
    {
        Transform dest = GetDestinationTransform();

        if(destinationTeleporter == this)
        {
            Debug.LogError($"{name}: destinationTeleporter is set to THIS teleporter. Assign a different TeleportCube.", this);
            return;
        }
        Debug.Log($"{name} teleporting to {(destinationTeleporter ? destinationTeleporter.name : destinationTransform ? destinationTransform.name : "NULL")} | destPos={dest.position}", this);

        if(dest == null)
        {
            Debug.LogWarning($"{name}: TeleportCube has no destination assigned.");
            return;
        }



        Rigidbody rb = player.GetComponent<Rigidbody>();

        Vector3 velocity = Vector3.zero;
        if(rb != null)
            velocity = rb.linearVelocity;

        // Teleport
        Vector3 arrivalPos = dest.position + Vector3.up * verticalOffset;
        player.transform.position = arrivalPos;
        // Cool down the destination too, to prevent immediate re-trigger
        if(destinationTeleporter != null)
            destinationTeleporter.coolingDown = true;
        if(snapRotation)
            player.transform.rotation = dest.rotation;

        if(rb != null)
        {
            rb.linearVelocity = preserveVelocity ? velocity : Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // One-way logic:
        // When the player ARRIVES, the destination disables itself
        if(oneWay && destinationTeleporter != null)
        {
            destinationTeleporter.SetActive(false);
        }

        // Optional: also disable THIS teleporter (true one-shot)
        if(oneWay && disableSelfOnUse)
        {
            SetActive(false);
        }

        StartCoroutine(CooldownRoutine());
    }

    private Transform GetDestinationTransform()
    {
        // 1) If explicitly provided, always use this
        if(destinationArrivalPoint != null)
            return destinationArrivalPoint;

        // 2) Otherwise, use the destination teleporter's arrival point (if set)
        if(destinationTeleporter != null)
        {
            if(destinationTeleporter == this)
                return null;

            if(destinationTeleporter.arrivalPoint != null)
                return destinationTeleporter.arrivalPoint;

            return destinationTeleporter.transform;
        }

        // 3) Fallback transform
        return destinationTransform;
    }

    private IEnumerator CooldownRoutine()
    {
        coolingDown = true;

        if(destinationTeleporter != null)
            destinationTeleporter.coolingDown = true;

        yield return new WaitForSeconds(reentryCooldown);

        coolingDown = false;

        if(destinationTeleporter != null)
            destinationTeleporter.coolingDown = false;
    }

#if UNITY_EDITOR
    [Header("Editor Gizmos")]
    public bool drawGizmos = true;
    public float lineThickness = 10f;
    public Color gizmoColor = new Color(0.2f, 0.6f, 1f, 1f);
    public float arrowSize = 0.35f;

    // NEW: separate gizmo height (closer to ground)
    public float gizmoYOffset = 0.1f;

    // NEW: if you ever want gizmos to aim at arrival points instead of the teleporter object
    public bool gizmoUseArrivalPoints = false;
    private void OnDrawGizmos()
    {
        if(!drawGizmos) return;
        if(!AnyTeleporterSelected()) return;
        DrawTeleportGizmos();
    }

    private static bool AnyTeleporterSelected()
    {
        var selected = Selection.gameObjects;
        if(selected == null || selected.Length == 0) return false;

        for(int i = 0; i < selected.Length; i++)
        {
            if(selected[i] == null) continue;
            if(selected[i].GetComponentInParent<TeleportCube>() != null)
                return true;
        }
        return false;
    }
    private void DrawTeleportGizmos()
    {
        // Destination should be the teleporter object itself (preferred),
        // OR destinationTransform (fallback), OR arrival points if you enable it.
        Transform dest = null;

        if(gizmoUseArrivalPoints)
        {
            dest = GetDestinationTransform(); // uses arrival logic
        }
        else
        {
            if(destinationTeleporter != null && destinationTeleporter != this)
                dest = destinationTeleporter.transform;
            else if(destinationTransform != null)
                dest = destinationTransform;
            else if(destinationArrivalPoint != null) // last resort if you've only set this
                dest = destinationArrivalPoint;
        }

        if(dest == null) return;

        var myCol = GetComponent<Collider>();
        Vector3 start = (myCol != null ? myCol.bounds.center : transform.position) + Vector3.up * gizmoYOffset;

        var destCol = dest.GetComponent<Collider>();
        Vector3 end = (destCol != null ? destCol.bounds.center : dest.position) + Vector3.up * gizmoYOffset;

        Vector3 v = end - start;
        float len = v.magnitude;
        if(len < 0.0001f) return;

        Vector3 dir = v / len;

        // Arrowhead at the DESTINATION end, pulled back slightly
        float arrowBack = Mathf.Clamp(arrowSize * 0.9f, 0.05f, len * 0.4f);
        Vector3 arrowPos = end - dir * arrowBack;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = gizmoColor;

        Handles.DrawAAPolyLine(lineThickness, start, end);

        float size = arrowSize + (lineThickness * 0.02f);
        Handles.ConeHandleCap(
            0,
            arrowPos,
            Quaternion.LookRotation(dir),
            size,
            EventType.Repaint
        );

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
    }
#endif
}
