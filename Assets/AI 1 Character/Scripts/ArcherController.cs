using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Longbow archery on top of the ThirdPersonController.
/// Right mouse: aim (shoulder camera, face the target, nock an arrow).
/// Left mouse: hold to pull the string - the longer and further the pull, the stronger the shot - and release to shoot.
/// A quick click shoots at the lowest power as soon as an arrow is on the string.
/// The script scrubs the draw clip itself (the Draw state's motion time is the DrawTime parameter).
/// Each frame after the animation it fits the bow into the closed bow hand, tilts the upper body towards the
/// aim point, keeps the string on the drawing hand, bends the bow with its "Draw" blendshape by how far the
/// string is pulled, places the nocked arrow and launches it at the release.
/// </summary>
[RequireComponent(typeof(ThirdPersonController))]
public class ArcherController : MonoBehaviour
{
    private static readonly int AimingHash = Animator.StringToHash("Aiming");
    private static readonly int FireHash = Animator.StringToHash("Fire");
    private static readonly int DrawTimeHash = Animator.StringToHash("DrawTime");
    private static readonly int FullDrawHash = Animator.StringToHash("FullDraw");
    private static readonly int EmptyStateHash = Animator.StringToHash("Empty");
    private static readonly int DrawStateHash = Animator.StringToHash("Draw");
    private static readonly int HoldStateHash = Animator.StringToHash("Hold");
    private static readonly int ReleaseStateHash = Animator.StringToHash("Release");

    private const int UpperBodyLayer = 1;
    private const float MaxAimDistance = 300f;
    // Spring for the string snapping back after the release
    private const float StringStiffness = 2500f;
    private const float StringDamping = 18f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private ThirdPersonCamera playerCamera;
    [Tooltip("Root of the bow (pivot at the grip, +Z = shooting direction). Its parent is fitted into the bow hand.")]
    [SerializeField] private Transform bow;
    [SerializeField] private SkinnedMeshRenderer bowRenderer;
    [SerializeField] private Transform stringNock;
    [Tooltip("Arrow shown on the string while drawing.")]
    [SerializeField] private Transform nockedArrow;
    [SerializeField] private GameObject arrowPrefab;

    [Header("Bow")]
    [Tooltip("How far the string moves back from rest at full draw (sets the Draw blendshape to 100).")]
    [SerializeField] private float fullDrawDistance = 0.48f;
    [Tooltip("Where the arrow rests on the bow, in bow space.")]
    [SerializeField] private Vector3 arrowRest = new Vector3(-0.015f, 0.05f, 0f);
    [Tooltip("How far the grip centre sits from the fingers towards the palm (0 = halfway between knuckles and fingertips).")]
    [SerializeField, Range(0f, 2f)] private float gripInset = 0.4f;
    [Tooltip("Slides the bow along the grip axis inside the fist (positive = bow moves up).")]
    [SerializeField] private float gripSlide = 0f;

    [Header("Shot")]
    [SerializeField] private float minArrowSpeed = 18f;
    [SerializeField] private float maxArrowSpeed = 60f;
    [Tooltip("Seconds to pull the string from the bow to the anchor.")]
    [SerializeField] private float pullTime = 0.7f;
    [Tooltip("Share of the power that comes from pulling to the anchor; the rest builds up while holding.")]
    [SerializeField, Range(0f, 1f)] private float pullPowerShare = 0.75f;
    [Tooltip("Seconds of holding at full draw to reach full power.")]
    [SerializeField] private float holdChargeTime = 1f;

    [Header("Draw clip timing (normalized)")]
    [SerializeField, Range(0f, 1f)] private float showArrowAt = 0.42f;
    [SerializeField, Range(0f, 1f)] private float grabStringFrom = 0.48f;
    [SerializeField, Range(0f, 1f)] private float grabStringUntil = 0.58f;
    [Tooltip("Arrow on the string, bow coming up: aiming waits here and the pull (left button) plays the rest.")]
    [SerializeField, Range(0f, 1f)] private float readyAt = 0.65f;
    [Tooltip("Where the next draw starts after a shot (skips reaching all the way down).")]
    [SerializeField, Range(0f, 1f)] private float reloadFrom = 0.2f;

    [Header("Pull")]
    [Tooltip("Easing the string back is this much faster than pulling.")]
    [SerializeField] private float letDownSpeed = 1.5f;

    [Header("Aiming")]
    [Tooltip("Yaw added to the camera direction so the bow, not the hips, points at the target.")]
    [SerializeField] private float aimYawOffset = 25.4f;
    [SerializeField] private float maxSpinePitch = 60f;

    private ThirdPersonController mover;
    private Transform[] spineBones;
    private Transform bowHand;
    private Transform bowSocket;
    private Vector3 gripForwardInHand;
    private Transform stringHand;
    private Transform stringHandTip;
    private float pull;
    private float yawCorrection;
    private Vector3 shotPosition;
    private Quaternion shotRotation;
    private float arrowPitchFromBow;
    private int drawBlendShape = -1;
    private float drawClipLength = 1f;
    private Vector3 stringRestLocal;
    private Vector3 stringOffset;
    private Vector3 stringVelocity;
    private float stringHold;
    private float aimWeight;
    private float drawTime;
    private float holdTime;
    private float shotPower;
    private bool aimHeld;
    private bool pullHeld;
    private bool engaged;
    private bool firePending;
    private bool shotQueued;
    private bool arrowOnString;
    private int upperState;
    private int lastUpperState;
    private Vector3 aimPoint;
    private bool inputOverridden;
    private bool overrideAim;
    private bool overridePull;

    /// <summary>Drive aim / pull from code (AI, cutscenes, tests) instead of the mouse. Call ClearInputOverride to go back.</summary>
    public void SetInputOverride(bool aim, bool pull)
    {
        inputOverridden = true;
        overrideAim = aim;
        overridePull = pull;
    }

    public void ClearInputOverride() => inputOverridden = false;

    /// <summary>0..1 strength of the shot if the string were released now.</summary>
    public float Power => upperState == HoldStateHash
        ? pullPowerShare + (1f - pullPowerShare) * Mathf.Clamp01(holdTime / holdChargeTime)
        : upperState == DrawStateHash ? pullPowerShare * pull : 0f;

    private void Awake()
    {
        mover = GetComponent<ThirdPersonController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        spineBones = new[]
        {
            animator.GetBoneTransform(HumanBodyBones.Spine),
            animator.GetBoneTransform(HumanBodyBones.Chest),
            animator.GetBoneTransform(HumanBodyBones.UpperChest)
        };
        // The string sits between the fingers, so pull it from the middle finger joints
        stringHand = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal) ?? animator.GetBoneTransform(HumanBodyBones.RightHand);
        stringHandTip = animator.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate) ?? stringHand;

        bowHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (bow != null)
        {
            bowSocket = bow.parent;
            gripForwardInHand = bowHand.InverseTransformDirection(bowSocket.forward);
        }

        if (stringNock != null) stringRestLocal = stringNock.localPosition;
        if (bowRenderer != null) drawBlendShape = bowRenderer.sharedMesh.GetBlendShapeIndex("Draw");
        if (nockedArrow != null) nockedArrow.gameObject.SetActive(false);

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == "standing draw arrow") drawClipLength = clip.length;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        bool canControl = mouse != null && Cursor.lockState == CursorLockMode.Locked;
        bool pullWasHeld = pullHeld;
        aimHeld = inputOverridden ? overrideAim : canControl && mouse.rightButton.isPressed;
        pullHeld = inputOverridden ? overridePull : canControl && mouse.leftButton.isPressed;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(UpperBodyLayer);
        upperState = state.shortNameHash;
        bool releasing = upperState == ReleaseStateHash;

        // Letting go of the left button always shoots: right away if an arrow is ready, otherwise as soon as one is
        if (pullWasHeld && !pullHeld) shotQueued = true;
        if (shotQueued) TryShoot();
        if (releasing) firePending = false;

        engaged = aimHeld || pullHeld || shotQueued || firePending || releasing;
        animator.SetBool(AimingHash, aimHeld || pullHeld || shotQueued || firePending);
        mover.IsAiming = engaged;
        mover.AimYawOffset = aimYawOffset + yawCorrection;
        if (playerCamera != null) playerCamera.IsAiming = aimHeld;

        UpdateDrawTime();
    }

    private void UpdateDrawTime()
    {
        if (upperState == EmptyStateHash && !animator.IsInTransition(UpperBodyLayer))
        {
            drawTime = 0f;
            pull = 0f;
        }
        if (upperState == HoldStateHash) holdTime += Time.deltaTime;
        else holdTime = 0f;

        if (!firePending && upperState != ReleaseStateHash && (aimHeld || pullHeld || shotQueued))
        {
            if (drawTime < readyAt)
            {
                // Nock an arrow at the clip's own pace
                drawTime = Mathf.MoveTowards(drawTime, readyAt, Time.deltaTime / drawClipLength);
            }
            else
            {
                // The rest of the clip is the pull: forward while the left button is held, back when it's let go
                float pullSpeed = (1f - readyAt) / pullTime;
                drawTime = pullHeld
                    ? Mathf.MoveTowards(drawTime, 1f, pullSpeed * Time.deltaTime)
                    : Mathf.MoveTowards(drawTime, readyAt, letDownSpeed * pullSpeed * Time.deltaTime);
            }
        }
        pull = Mathf.InverseLerp(readyAt, 1f, drawTime);

        animator.SetFloat(DrawTimeHash, drawTime);
        animator.SetBool(FullDrawHash, pullHeld && drawTime >= 0.999f);
    }

    private void TryShoot()
    {
        bool ready = upperState == HoldStateHash || upperState == DrawStateHash && drawTime >= readyAt;
        if (!ready || !arrowOnString || animator.IsInTransition(UpperBodyLayer)) return;

        // Remember the arrow as it is aimed now; the release clip moves the hand before the arrow leaves
        shotPower = Power;
        shotPosition = nockedArrow.position;
        shotRotation = nockedArrow.rotation;
        shotQueued = false;
        firePending = true;
        animator.SetTrigger(FireHash);
    }

    private void LateUpdate()
    {
        bool drawn = upperState == HoldStateHash;
        bool released = upperState == ReleaseStateHash;
        float draw = upperState == DrawStateHash ? drawTime : 0f;

        aimPoint = FindAimPoint();

        // Upper body follows the aim pitch while the bow is up
        bool bowUp = engaged && (drawn || draw > 0.4f) || released && animator.GetCurrentAnimatorStateInfo(UpperBodyLayer).normalizedTime < 0.4f;
        aimWeight = Mathf.MoveTowards(aimWeight, bowUp ? 1f : 0f, 4f * Time.deltaTime);
        ApplySpinePitch();
        FitBowToHand();

        if (released && lastUpperState != ReleaseStateHash)
        {
            if (arrowOnString) LaunchArrow();
            drawTime = reloadFrom;
            pull = 0f;
        }
        lastUpperState = upperState;

        // How much the drawing hand holds the string: grabbed during the draw, let go at the release
        float holdTarget = 0f;
        if (drawn) holdTarget = 1f;
        else if (upperState == DrawStateHash && engaged) holdTarget = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(grabStringFrom, grabStringUntil, draw));
        stringHold = released ? 0f : holdTarget;

        UpdateString();
        UpdateNockedArrow(engaged && !released && (drawn || upperState == DrawStateHash && draw >= showArrowAt));
        CorrectAimFromArrow();
    }

    private Vector3 FindAimPoint()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position + transform.forward * MaxAimDistance;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 point = ray.GetPoint(MaxAimDistance);
        float closest = MaxAimDistance;
        foreach (RaycastHit hit in Physics.RaycastAll(ray, MaxAimDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.distance >= closest) continue;
            closest = hit.distance;
            point = hit.point;
        }
        return point;
    }

    private void ApplySpinePitch()
    {
        if (aimWeight <= 0f || bow == null) return;

        // Pitch between the current bow line and the line to the aim point, spread over the spine bones
        // The arrow line differs a little from the bow's forward; use last frame's difference so the arrow hits the aim point
        Vector3 toTarget = aimPoint - bow.position;
        float current = PitchOf(bow.forward) + arrowPitchFromBow;
        float wanted = PitchOf(toTarget);
        float pitch = Mathf.Clamp(wanted - current, -maxSpinePitch, maxSpinePitch) * aimWeight;

        int count = 0;
        foreach (Transform bone in spineBones) if (bone != null) count++;
        foreach (Transform bone in spineBones)
        {
            if (bone == null) continue;
            // Positive pitch = aim up = rotate backwards around the character's right axis
            bone.rotation = Quaternion.AngleAxis(-pitch / count, transform.right) * bone.rotation;
        }
    }

    private static float PitchOf(Vector3 direction) => Mathf.Asin(Mathf.Clamp(direction.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;

    /// <summary>Turns the body and the spine pitch towards where the nocked arrow actually points.</summary>
    private void CorrectAimFromArrow()
    {
        if (nockedArrow == null || !nockedArrow.gameObject.activeSelf) return;

        arrowPitchFromBow = PitchOf(nockedArrow.forward) - PitchOf(bow.forward);
        if (drawTime < readyAt && upperState != HoldStateHash) return;

        Vector3 arrowFlat = Vector3.ProjectOnPlane(nockedArrow.forward, Vector3.up);
        Vector3 targetFlat = Vector3.ProjectOnPlane(aimPoint - nockedArrow.position, Vector3.up);
        float yawError = Vector3.SignedAngle(arrowFlat, targetFlat, Vector3.up);
        yawCorrection = Mathf.Clamp(yawCorrection + yawError * Mathf.Clamp01(10f * Time.deltaTime), -45f, 45f);
    }

    private void FitBowToHand()
    {
        if (bowSocket == null) return;

        // The grip goes through the middle of the curled index, middle and ring fingers, along the knuckles
        Vector3 centre = FingerCurlCentre(HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal)
                       + FingerCurlCentre(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal)
                       + FingerCurlCentre(HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal);
        centre /= 3f;
        Vector3 up = (animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal).position
                      - animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal).position).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(bowHand.TransformDirection(gripForwardInHand), up).normalized;

        bowSocket.SetPositionAndRotation(centre + up * gripSlide, Quaternion.LookRotation(forward, up));
    }

    private Vector3 FingerCurlCentre(HumanBodyBones proximal, HumanBodyBones intermediate, HumanBodyBones distal)
    {
        Vector3 a = animator.GetBoneTransform(proximal).position;
        Vector3 b = animator.GetBoneTransform(intermediate).position;
        Vector3 c = animator.GetBoneTransform(distal).position;
        Vector3 mid = (a + c) * 0.5f;
        return mid + (mid - b) * gripInset;
    }

    private void UpdateString()
    {
        if (stringNock == null) return;

        Transform parent = stringNock.parent;
        if (stringHold > 0f)
        {
            Vector3 handLocal = parent.InverseTransformPoint(StringHandPoint());
            Vector3 target = Vector3.Lerp(stringRestLocal, handLocal, stringHold) - stringRestLocal;
            stringVelocity = (target - stringOffset) / Mathf.Max(Time.deltaTime, 0.0001f);
            stringOffset = target;
        }
        else
        {
            // Released: spring back to rest and wobble out
            stringVelocity += (-StringStiffness * stringOffset - StringDamping * stringVelocity) * Time.deltaTime;
            stringOffset += stringVelocity * Time.deltaTime;
        }
        stringNock.localPosition = stringRestLocal + stringOffset;

        if (bowRenderer != null && drawBlendShape >= 0)
        {
            Vector3 restWorld = parent.TransformPoint(stringRestLocal);
            float pulled = Vector3.Dot(restWorld - stringNock.position, bow.forward);
            bowRenderer.SetBlendShapeWeight(drawBlendShape, Mathf.Clamp01(pulled / fullDrawDistance) * 100f);
        }
    }

    private Vector3 StringHandPoint() => Vector3.Lerp(stringHand.position, stringHandTip.position, 0.5f);

    private void UpdateNockedArrow(bool visible)
    {
        arrowOnString = visible;
        if (nockedArrow == null) return;
        nockedArrow.gameObject.SetActive(visible);
        if (!visible) return;
        if (firePending)
        {
            nockedArrow.SetPositionAndRotation(shotPosition, shotRotation);
            return;
        }

        // Nock end on the string (or in the hand before it reaches the string), pointing through the arrow rest
        Vector3 nock = stringHold > 0.5f ? stringNock.position : StringHandPoint();
        Vector3 rest = bow.TransformPoint(arrowRest);
        nockedArrow.SetPositionAndRotation(nock, Quaternion.LookRotation(rest - nock, bow.up));
    }

    private void LaunchArrow()
    {
        if (arrowPrefab == null || nockedArrow == null) return;

        // Fly exactly where the nocked arrow pointed at the release (the aim correction keeps that on the crosshair)
        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, shotPower);
        Vector3 direction = shotRotation * Vector3.forward;
        GameObject arrow = Instantiate(arrowPrefab, shotPosition, shotRotation);
        arrow.AddComponent<ArrowProjectile>().Launch(direction * speed, transform);

        nockedArrow.gameObject.SetActive(false);
        arrowOnString = false;
    }

    private void OnGUI()
    {
        if (!engaged) return;

        // Crosshair whose arms close in as the shot gets stronger
        float x = Screen.width * 0.5f;
        float y = Screen.height * 0.5f;
        float gap = Mathf.Lerp(10f, 2f, Power);
        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.DrawTexture(new Rect(x - 1f, y - gap - 5f, 2f, 5f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 1f, y + gap, 2f, 5f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - gap - 5f, y - 1f, 5f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + gap, y - 1f, 5f, 2f), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
