using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Third person camera, child of the player. Mouse X rotates the whole player (so movement follows the camera),
/// mouse wheel zooms. Close to the player the view is nearly horizontal, further away it tilts down up to 45 degrees.
/// </summary>
public class PlayerCamera : MonoBehaviour
{
    private const float SmoothingSpeed = 10f;
    private const float MaxElevation = 45f;

    [SerializeField] private float minCloseUp = 1f;
    [SerializeField] private float maxDistance = 50f;

    [Tooltip("Distance at which the camera reaches the 45° elevation and stays there when zooming out further.")]
    [SerializeField]
    private float maxClosingUpDistance = 10f;

    [Tooltip("Height of the camera above the player at minCloseUp. Fades out while zooming out.")] [SerializeField]
    private float closeUpHeight = 0.5f;

    [SerializeField] private float rotationSpeed = 0.2f;
    [SerializeField] private float zoomSpeed = 2f;

    private Transform player;
    private float targetYaw;
    private float yaw;
    private float targetDistance;
    private float distance;

    private void Start()
    {
        player = transform.parent;
        if (player == null)
        {
            Debug.LogError("PlayerCamera must be a child of the player.");
            enabled = false;
            return;
        }

        // Camera sits behind the player in local space; keep the distance it has in the scene
        distance = targetDistance = Mathf.Clamp(transform.localPosition.magnitude, minCloseUp, maxDistance);
    }

    private void LateUpdate()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        
        HandleMouseMovement(mouse);
    }

    private void HandleMouseMovement(Mouse mouse)
    {
        // Only the horizontal axis matters
        targetYaw += mouse.delta.ReadValue().x * rotationSpeed;
        
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f)
        {
            targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(scroll) * zoomSpeed, minCloseUp, maxDistance);
        }
        
        float t = 1f - Mathf.Exp(-SmoothingSpeed * Time.deltaTime);
        
        // Rotate the whole player by the smoothed yaw difference
        float newYaw = Mathf.Lerp(yaw, targetYaw, t);
        player.Rotate(0f, newYaw - yaw, 0f, Space.World);
        yaw = newYaw;
        
        distance = Mathf.Lerp(distance, targetDistance, t);
        
        // 0 at minCloseUp, 1 at maxClosingUpDistance and further
        float zoomOut = Mathf.InverseLerp(minCloseUp, maxClosingUpDistance, distance);
        float elevation = Mathf.Lerp(0f, MaxElevation, zoomOut);
        
        // Offset in the player's local space, camera stays behind the player
        Vector3 offset = Quaternion.Euler(elevation, 0f, 0f) * Vector3.back * distance;
        offset.y += closeUpHeight * (1f - zoomOut);
        
        transform.localPosition = offset;
        transform.localRotation = Quaternion.LookRotation(-offset);
    }
}