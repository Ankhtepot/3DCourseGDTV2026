using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Third person player movement on a CharacterController. WASD / arrows walk relative to the camera,
/// Caps Lock toggles running, Shift sprints, Space jumps. The character turns to face the direction it moves in
/// and drives the Animator parameters Speed, MoveX, MoveY, Grounded, VerticalSpeed and Jump.
/// While IsAiming is set (by the ArcherController) the character faces the camera direction and strafes at aim speed.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");

    // Small constant push into the ground so isGrounded stays true on slopes and steps
    private const float GroundedGravity = -2f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.8f;
    [SerializeField] private float runSpeed = 4f;
    [SerializeField] private float sprintSpeed = 6.5f;
    [Tooltip("How fast the speed changes, in m/s per second.")]
    [SerializeField] private float acceleration = 12f;
    [Tooltip("Time in seconds to turn towards the movement direction.")]
    [SerializeField] private float turnSmoothTime = 0.1f;
    [Tooltip("How much the player can steer while in the air (0 = none, 1 = full).")]
    [SerializeField, Range(0f, 1f)] private float airControl = 0.5f;

    [Header("Aiming")]
    [SerializeField] private float aimMoveSpeed = 1.8f;
    [Tooltip("Time in seconds to turn towards the camera direction while aiming.")]
    [SerializeField] private float aimTurnSmoothTime = 0.05f;

    [Header("Jumping")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("Jump still works this long after walking off a ledge.")]
    [SerializeField] private float coyoteTime = 0.15f;
    [Tooltip("A jump pressed this long before landing still triggers.")]
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("References")]
    [Tooltip("Movement is relative to this camera. Defaults to Camera.main.")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;
    [Tooltip("Damping time for the animator Speed parameter.")]
    [SerializeField] private float animationDamping = 0.1f;
    [Tooltip("Turning on the spot at this many degrees per second plays a full stepping animation.")]
    [SerializeField] private float turnStepRate = 180f;
    [Tooltip("Speed fed to the animator for stepping while turning on the spot.")]
    [SerializeField] private float turnStepSpeed = 0.9f;

    private CharacterController controller;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float turnVelocity;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private bool isJumping;
    private bool runToggled;
    private float lastYaw;

    public bool IsGrounded { get; private set; }
    public bool IsAiming { get; set; }
    public float HorizontalSpeed => horizontalVelocity.magnitude;

    /// <summary>Raised when a jump starts.</summary>
    public event System.Action Jumped;
    /// <summary>Raised on touching the ground after being in the air, with the downward speed at impact.</summary>
    public event System.Action<float> Landed;
    /// <summary>Yaw added to the camera direction while aiming, so the bow (not the hips) points at the target.</summary>
    public float AimYawOffset { get; set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        // Movement comes from this script, the clips are played in place
        if (animator != null) animator.applyRootMotion = false;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector2 input = ReadMoveInput(keyboard);
        bool sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        if (keyboard.capsLockKey.wasPressedThisFrame) runToggled = !runToggled;
        if (keyboard.spaceKey.wasPressedThisFrame) lastJumpPressedTime = Time.time;

        UpdateGrounded();
        UpdateHorizontal(input, sprint);
        UpdateVertical();

        controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        UpdateAnimator();
    }

    private static Vector2 ReadMoveInput(Keyboard keyboard)
    {
        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        return Vector2.ClampMagnitude(input, 1f);
    }

    /// <summary>Stops all movement and pending jumps, e.g. after the player has been teleported.</summary>
    public void ResetMotion()
    {
        horizontalVelocity = Vector3.zero;
        verticalVelocity = GroundedGravity;
        isJumping = false;
        lastJumpPressedTime = float.NegativeInfinity;
    }

    private void UpdateGrounded()
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = controller.isGrounded;
        if (IsGrounded)
        {
            if (!wasGrounded) Landed?.Invoke(-verticalVelocity);
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f) isJumping = false;
        }
    }

    private void UpdateHorizontal(Vector2 input, bool sprint)
    {
        // Flatten the camera axes so looking up or down doesn't slow the player
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }

        Vector3 moveDirection = Vector3.zero;
        if (input.sqrMagnitude > 0.01f) moveDirection = forward * input.y + right * input.x;

        if (IsAiming)
        {
            // Face where the camera looks and strafe
            float targetAngle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg + AimYawOffset;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, aimTurnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
        else if (moveDirection != Vector3.zero)
        {
            float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        float targetSpeed = IsAiming ? aimMoveSpeed : sprint ? sprintSpeed : runToggled ? runSpeed : walkSpeed;
        Vector3 targetVelocity = moveDirection * targetSpeed;

        float control = IsGrounded ? 1f : airControl;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * control * Time.deltaTime);
    }

    private void UpdateVertical()
    {
        bool canJump = !isJumping && Time.time - lastGroundedTime <= coyoteTime;
        bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;

        if (canJump && jumpBuffered)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isJumping = true;
            lastJumpPressedTime = float.NegativeInfinity;
            if (animator != null) animator.SetTrigger(JumpHash);
            Jumped?.Invoke();
        }
        else if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = GroundedGravity;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        // Turning on the spot: step the feet instead of sliding around (a few steps in place / to the side)
        float yaw = transform.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastYaw = yaw;
        float speed = horizontalVelocity.magnitude;
        float turnStep = IsGrounded ? Mathf.Clamp01(Mathf.Abs(yawRate) / turnStepRate) * Mathf.Clamp01(1f - speed / turnStepSpeed) : 0f;

        animator.SetFloat(SpeedHash, Mathf.Max(speed, turnStep * turnStepSpeed), animationDamping * 2f, Time.deltaTime);

        // Velocity in the character's own space drives the strafe blend while aiming; turning steps sideways into the turn
        Vector3 localVelocity = transform.InverseTransformDirection(horizontalVelocity);
        float stepSide = Mathf.Sign(yawRate) * turnStep * turnStepSpeed;
        animator.SetFloat(MoveXHash, Mathf.Abs(stepSide) > Mathf.Abs(localVelocity.x) ? stepSide : localVelocity.x, animationDamping * 2f, Time.deltaTime);
        animator.SetFloat(MoveYHash, localVelocity.z, animationDamping, Time.deltaTime);
        animator.SetBool(GroundedHash, IsGrounded);
        animator.SetFloat(VerticalSpeedHash, verticalVelocity);
    }
}
