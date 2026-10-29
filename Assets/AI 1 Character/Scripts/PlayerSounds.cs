using UnityEngine;

/// <summary>
/// Footsteps, jump and landing sounds for the third person player, played through the AudioPool.
/// Footsteps follow the distance walked, with a longer stride at higher speed, and sound softer on terrain than on stone.
/// </summary>
[RequireComponent(typeof(ThirdPersonController))]
public class PlayerSounds : MonoBehaviour
{
    [SerializeField] private float walkStride = 0.75f;
    [SerializeField] private float sprintStride = 1.35f;
    [SerializeField] private float footstepVolume = 0.35f;
    [SerializeField] private float jumpVolume = 0.25f;
    [SerializeField] private float landVolume = 0.55f;
    [Tooltip("Landings slower than this (m/s) only make a footstep.")]
    [SerializeField] private float hardLandingSpeed = 5f;

    private ThirdPersonController mover;
    private StepSmoother stepper;
    private Vector3 lastPosition;
    private float distance;

    private void Awake()
    {
        mover = GetComponent<ThirdPersonController>();
        stepper = GetComponentInChildren<StepSmoother>();
    }

    private void OnEnable()
    {
        mover.Jumped += OnJumped;
        mover.Landed += OnLanded;
        if (stepper != null) stepper.Stepped += OnStepped;
        lastPosition = transform.position;
    }

    private void OnDisable()
    {
        mover.Jumped -= OnJumped;
        mover.Landed -= OnLanded;
        if (stepper != null) stepper.Stepped -= OnStepped;
    }

    private void Update()
    {
        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;
        moved.y = 0f;
        if (!mover.enabled || !mover.IsGrounded || moved.magnitude > 1f) return;

        float speed = mover.HorizontalSpeed;
        float pace = Mathf.InverseLerp(1.8f, 6.5f, speed);
        distance += moved.magnitude;
        if (distance >= Mathf.Lerp(walkStride, sprintStride, pace))
        {
            distance = 0f;
            Footstep(Mathf.Lerp(0.6f, 1f, pace));
        }
    }

    private void OnJumped()
    {
        Footstep(1f);
        AudioPool.Play(SoundSynth.Jump(), jumpVolume, Random.Range(0.9f, 1.15f));
    }

    private void OnLanded(float impactSpeed)
    {
        distance = 0f;
        Footstep(1f);
        if (impactSpeed > hardLandingSpeed)
            AudioPool.Play(SoundSynth.Land(), landVolume * Mathf.InverseLerp(hardLandingSpeed, 15f, impactSpeed) + 0.15f, Random.Range(0.9f, 1.05f));
    }

    private void OnStepped(float height)
    {
        if (height > 0f) Footstep(0.9f);
    }

    private void Footstep(float loudness)
    {
        AudioClip clip = IsOnTerrain() ? SoundSynth.SoftStep() : SoundSynth.StoneStep();
        AudioPool.Play(clip, footstepVolume * loudness, Random.Range(0.88f, 1.12f));
    }

    private bool IsOnTerrain()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 1f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider is TerrainCollider;
    }
}
