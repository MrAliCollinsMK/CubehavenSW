using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CubePlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float acceleration = 25f;
    public float airControl = 8f;

    [Header("Jump")]
    public float jumpForce = 7f;
    public float coyoteTime = 0.1f;
    public float jumpBuffer = 0.1f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.25f;
    public LayerMask groundMask = ~0;

    Rigidbody rb;

    float coyoteTimer;
    float jumpBufferTimer;
    bool jumpPressedThisFrame;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.freezeRotation = true;
    }

    void Update()
    {
        // Read jump (Space) using new input system
        jumpPressedThisFrame = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        if(jumpPressedThisFrame)
            jumpBufferTimer = jumpBuffer;

        jumpBufferTimer -= Time.deltaTime;
        coyoteTimer -= Time.deltaTime;

        bool grounded = IsGrounded();
        if(grounded)
            coyoteTimer = coyoteTime;

        if(jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            Jump();
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }
    }

    void FixedUpdate()
    {
        Vector2 move = ReadMove(); // WASD / arrows
        Vector3 input = new Vector3(move.x, 0f, move.y);
        if(input.sqrMagnitude > 1f) input.Normalize();

        Vector3 targetVel = input * moveSpeed;
        Vector3 vel = rb.linearVelocity;

        float accel = IsGrounded() ? acceleration : airControl;

        Vector3 velXZ = new Vector3(vel.x, 0f, vel.z);
        Vector3 newVelXZ = Vector3.MoveTowards(velXZ, targetVel, accel * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(newVelXZ.x, vel.y, newVelXZ.z);
    }

    Vector2 ReadMove()
    {
        if(Keyboard.current == null) return Vector2.zero;

        float x = 0f;
        float y = 0f;

        if(Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
        if(Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;

        if(Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
        if(Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;

        return new Vector2(x, y);
    }

    void Jump()
    {
        Vector3 v = rb.linearVelocity;
        if(v.y < 0f) v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    bool IsGrounded()
    {
        Vector3 pos = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.down * 0.55f;

        return Physics.CheckSphere(pos, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 pos = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.down * 0.55f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, groundCheckRadius);
    }
}