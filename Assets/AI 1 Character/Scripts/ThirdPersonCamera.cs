using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Orbit camera for a third person player. Mouse moves the camera around the target, the mouse wheel zooms
/// and the camera pulls in when a wall or the ground is between it and the player.
/// The cursor is locked while playing; Escape frees it, clicking into the game view locks it again.
/// While IsAiming is set the camera moves in over the right shoulder.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    private const float ZoomSmoothing = 10f;
    private const float CollisionRadius = 0.2f;

    [SerializeField] private Transform target;
    [Tooltip("Point the camera looks at, relative to the target's pivot (roughly shoulder height).")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Orbit")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;
    [SerializeField] private bool invertY;

    [Header("Zoom")]
    [SerializeField] private float distance = 4f;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 10f;
    [Tooltip("Distance change per mouse wheel notch.")]
    [SerializeField] private float zoomStep = 0.75f;

    [Header("Aiming")]
    [SerializeField] private float aimDistance = 1.8f;
    [Tooltip("Sideways offset of the look-at point while aiming (positive = over the right shoulder).")]
    [SerializeField] private float aimShoulderOffset = 0.5f;
    [SerializeField] private float aimBlendSpeed = 10f;

    [Header("Collision")]
    [Tooltip("Layers that block the camera. Exclude the player's own layer.")]
    [SerializeField] private LayerMask collisionLayers = ~0;

    private float yaw;
    private float pitch = 15f;
    private float targetDistance;
    private float currentDistance;
    private float aimBlend;

    public bool IsAiming { get; set; }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError("ThirdPersonCamera needs a target.");
            enabled = false;
            return;
        }

        yaw = target.eulerAngles.y;
        targetDistance = currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        LockCursor(true);
    }

    private void LateUpdate()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) LockCursor(false);
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) LockCursor(true);

        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
            yaw += delta.x;
            pitch += invertY ? delta.y : -delta.y;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
            }
        }

        currentDistance = Mathf.Lerp(currentDistance, targetDistance, 1f - Mathf.Exp(-ZoomSmoothing * Time.deltaTime));
        aimBlend = Mathf.MoveTowards(aimBlend, IsAiming ? 1f : 0f, aimBlendSpeed * Time.deltaTime);

        GetPose(out Vector3 position, out Quaternion rotation);
        transform.SetPositionAndRotation(position, rotation);
    }

    /// <summary>Turns the orbit to this yaw and pitch, e.g. to face the course after a respawn.</summary>
    public void SetOrientation(float newYaw, float newPitch)
    {
        yaw = newYaw;
        pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
    }

    /// <summary>Where the camera sits for the current orbit, zoom and aim, pulled in front of obstacles.</summary>
    public void GetPose(out Vector3 position, out Quaternion rotation)
    {
        float t = Mathf.SmoothStep(0f, 1f, aimBlend);
        float distanceNow = Mathf.Lerp(currentDistance, aimDistance, t);

        rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + targetOffset + rotation * Vector3.right * (aimShoulderOffset * t);
        Vector3 direction = rotation * Vector3.back;

        // Pull the camera in front of anything between it and the player
        float allowedDistance = distanceNow;
        if (Physics.SphereCast(pivot, CollisionRadius, direction, out RaycastHit hit, distanceNow,
                collisionLayers, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(target))
        {
            allowedDistance = hit.distance;
        }

        position = pivot + direction * allowedDistance;
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
