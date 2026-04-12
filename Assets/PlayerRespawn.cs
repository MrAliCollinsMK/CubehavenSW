using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    private Vector3 initialSpawn;
    private Vector3 currentCheckpoint;
    private bool hasCheckpoint = false;

    CharacterController cc;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        initialSpawn = transform.position;
    }

    public void SetCheckpoint(Vector3 checkpointPos)
    {
        currentCheckpoint = checkpointPos;
        hasCheckpoint = true;
    }

    public void Respawn()
    {
        Vector3 targetPos = hasCheckpoint ? currentCheckpoint : initialSpawn;

        // Disable CC so Move doesn’t fight teleport
        cc.enabled = false;
        transform.position = targetPos;
        cc.enabled = true;
    }
}