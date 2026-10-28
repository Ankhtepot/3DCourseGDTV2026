using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Orbit camera for a third person player. Mouse moves the camera around the target, the mouse wheel zooms
/// and the camera pulls in when a wall or the ground is between it and the player.
/// The cursor is locked while playing; Escape frees it, clicking into the game view locks it again.
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

    [Header("Collision")]
    [Tooltip("Layers that block the camera. Exclude the player's own layer.")]
    [SerializeField] private LayerMask collisionLayers = ~0;

    private float yaw;
    private float pitch = 15f;
    private float targetDistance;
    private float currentDistance;

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

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + targetOffset;
        Vector3 direction = rotation * Vector3.back;

        // Pull the camera in front of anything between it and the player
        float allowedDistance = currentDistance;
        if (Physics.SphereCast(pivot, CollisionRadius, direction, out RaycastHit hit, currentDistance,
                collisionLayers, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(target))
        {
            allowedDistance = hit.distance;
        }

        transform.SetPositionAndRotation(pivot + direction * allowedDistance, rotation);
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
