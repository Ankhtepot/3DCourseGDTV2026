using UnityEngine;

/// <summary>
/// Procedural step animation. The CharacterController pops up or down a step in a single frame; this keeps the
/// visible model where it was and eases it to the new height, and swings the trailing foot up over the step edge
/// with IK. Goes on the GameObject with the Animator, whose base layer needs IK Pass turned on.
/// </summary>
[RequireComponent(typeof(Animator))]
public class StepSmoother : MonoBehaviour
{
    [Tooltip("Height changes smaller than this are ignored.")]
    [SerializeField] private float minStep = 0.05f;
    [Tooltip("Height changes bigger than this are teleports and aren't smoothed.")]
    [SerializeField] private float maxStep = 0.8f;
    [Tooltip("A height change counts as a step only when it's this many times steeper than the distance moved (slopes don't).")]
    [SerializeField] private float minSteepness = 1.2f;
    [Tooltip("Time for the body to catch up with the step.")]
    [SerializeField] private float smoothTime = 0.08f;

    [Header("Foot lift")]
    [SerializeField] private float liftDuration = 0.3f;
    [Tooltip("How high the foot swings, as a share of the step height.")]
    [SerializeField] private float liftHeight = 0.8f;
    [Tooltip("How far the foot reaches forward at the top of the swing.")]
    [SerializeField] private float liftReach = 0.15f;

    private Animator animator;
    private ThirdPersonController mover;
    private Transform body;
    private Vector3 restLocalPosition;
    private Vector3 lastPosition;
    private float offset;
    private float offsetVelocity;
    private bool wasGrounded;

    private AvatarIKGoal liftFoot;
    private float liftStart = -1f;
    private float liftAmount;

    /// <summary>Raised when the character steps up (positive) or down (negative), with the height.</summary>
    public event System.Action<float> Stepped;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        mover = GetComponentInParent<ThirdPersonController>();
        body = mover != null ? mover.transform : transform.parent;
        restLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        if (body != null) lastPosition = body.position;
        offset = offsetVelocity = 0f;
    }

    private void LateUpdate()
    {
        Vector3 position = body.position;
        Vector3 moved = position - lastPosition;
        lastPosition = position;

        float rise = moved.y;
        moved.y = 0f;
        bool grounded = mover != null && mover.enabled && mover.IsGrounded;
        bool isStep = Mathf.Abs(rise) > minStep && Mathf.Abs(rise) < maxStep && Mathf.Abs(rise) > moved.magnitude * minSteepness;
        if (grounded && wasGrounded && isStep)
        {
            offset -= rise;
            if (rise > 0f) StartFootLift(rise);
            Stepped?.Invoke(rise);
        }
        wasGrounded = grounded;

        offset = Mathf.Clamp(Mathf.SmoothDamp(offset, 0f, ref offsetVelocity, smoothTime), -maxStep, maxStep);
        transform.localPosition = restLocalPosition + Vector3.up * offset;
    }

    private void StartFootLift(float height)
    {
        // The foot behind is the one that swings forward onto the step
        Vector3 forward = body.forward;
        float left = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position - body.position, forward);
        float right = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.RightFoot).position - body.position, forward);
        liftFoot = left < right ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightFoot;
        liftAmount = height;
        liftStart = Time.time;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || liftStart < 0f) return;

        float t = (Time.time - liftStart) / liftDuration;
        if (t >= 1f)
        {
            liftStart = -1f;
            animator.SetIKPositionWeight(liftFoot, 0f);
            return;
        }

        float swing = Mathf.Sin(t * Mathf.PI);
        Vector3 target = animator.GetIKPosition(liftFoot) + Vector3.up * (liftAmount * liftHeight * swing) + body.forward * (liftReach * swing);
        animator.SetIKPosition(liftFoot, target);
        animator.SetIKPositionWeight(liftFoot, swing);
    }
}
