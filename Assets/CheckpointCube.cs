using UnityEngine;

public class CheckpointCube : MonoBehaviour
{
    private bool activated = false;
    Vector3 mVec;

    [Header("Beam FX")]
    public LineRenderer beam;
    public float beamHeight = 6f;
    public float beamTime = 1f;

    private void OnTriggerEnter(Collider other)
    {
        if(activated) return;

        PlayerRespawn respawn = other.GetComponentInParent<PlayerRespawn>();
        if(respawn == null) return;
        mVec = transform.position;
        mVec.y += 1;
        respawn.SetCheckpoint(mVec);
        activated = true;

        if(beam != null)
        {
            StopAllCoroutines();
            StartCoroutine(BeamRoutine());
        }

        // Optional: visual feedback
        // gameObject.SetActive(false);
        // or play animation / glow / sound
    }
    System.Collections.IEnumerator BeamRoutine()
    {
        beam.enabled = true;

        Vector3 basePos = transform.position;
        beam.positionCount = 2;
        beam.SetPosition(0, basePos);
        beam.SetPosition(1, basePos + Vector3.up * beamHeight);

        yield return new WaitForSeconds(beamTime);

        beam.enabled = false;
    }
}
