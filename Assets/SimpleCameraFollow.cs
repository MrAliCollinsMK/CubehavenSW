using UnityEngine;
using System.Collections;

public class SimpleCameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Iso Settings")]
    public float pitch = 30f;
    public float yaw = -135f;              // starting yaw
    public float distance = 12f;           // how far from target along forward
    public float followHeight = 0f;        // optional extra height (usually 0)

    [Header("Motion")]
    public float followLerp = 12f;         // positional smoothing
    public float rotateDuration = 0.65f;   // smooth rotate time

    bool isRotating;

    void Start()
    {
        ApplyImmediate();
    }

    void ApplyImmediate()
    {
        if(!target) return;

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rot;

        // Put the camera "behind" its forward direction so it looks at target
        Vector3 desiredPos = target.position - (rot * Vector3.forward) * distance;
        desiredPos += Vector3.up * followHeight;

        transform.position = desiredPos;
    }

    // Public API
    public void Flip180() => RotateYawSmooth(180f);
    public void Rotate90CW() => RotateYawSmooth(90f);
    public void Rotate90CCW() => RotateYawSmooth(-90f);

    public void RotateYawSmooth(float deltaYaw)
    {
        if(!target || isRotating) return;
        StartCoroutine(RotateCoroutine(deltaYaw));
    }
    float yawVelocity; // must be persistent

    IEnumerator RotateCoroutine(float deltaYaw)
    {
        isRotating = true;

        float targetYaw = yaw + deltaYaw;

        while(Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw)) > 0.05f)
        {
            yaw = Mathf.SmoothDampAngle(
                yaw,
                targetYaw,
                ref yawVelocity,
                rotateDuration,
                Mathf.Infinity,
                Time.deltaTime
            );

            yield return null;
        }

        yaw = targetYaw;
        yawVelocity = 0f;
        isRotating = false;
    }

    void LateUpdate()
    {
        if(!target) return;

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rot;

        Vector3 desiredPos = target.position - (rot * Vector3.forward) * distance;
        desiredPos += Vector3.up * followHeight;

        transform.position = Vector3.Lerp(transform.position, desiredPos, followLerp * Time.deltaTime);
    }

    // quick test keys
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.F)) Flip180();
        if(Input.GetKeyDown(KeyCode.U)) Rotate90CW();
        if(Input.GetKeyDown(KeyCode.Q)) Rotate90CCW();
    }
}