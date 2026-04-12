using UnityEngine;

public class DoorBlock : LogicBlock
{
    [Header("Door")]
    public Transform doorPivot;              // what actually rotates
    public Vector3 closedEuler;               // local rotation when OFF
    public Vector3 openEuler;                 // local rotation when ON
    public float rotateSpeed = 180f;           // degrees per second

    Quaternion targetRotation;

    void Start()
    {
        if(doorPivot == null)
            doorPivot = transform;

        // Set initial rotation based on starting state
        targetRotation = Quaternion.Euler(
            currentState == LogicState.On ? openEuler : closedEuler
        );

        doorPivot.localRotation = targetRotation;
    }

    protected override void ApplyState()
    {
        // Optional: still allow base visuals if you want
        base.ApplyState();

        // Choose target rotation
        targetRotation = Quaternion.Euler(
            currentState == LogicState.On ? openEuler : closedEuler
        );
    }

    void Update()
    {
        if(doorPivot == null) return;

        doorPivot.localRotation = Quaternion.RotateTowards(
            doorPivot.localRotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }
}