using UnityEngine;

/// <summary>
/// Platform that eases back and forth along a travel vector and carries the player standing on it.
/// Runs before the player's controller so the player's own movement is applied on top.
/// </summary>
[DefaultExecutionOrder(-10)]
public class SlidingPlatform : MonoBehaviour
{
    [Tooltip("Offset from the start position at the far end of the travel.")]
    [SerializeField] private Vector3 travel = new Vector3(0f, -2.2f, 0f);
    [Tooltip("Seconds for a full trip there and back.")]
    [SerializeField] private float period = 5f;
    [Tooltip("Collider the player stands on.")]
    [SerializeField] private Collider top;

    private Vector3 start;

    private void Start()
    {
        start = transform.position;
    }

    private void Update()
    {
        float t = (1f - Mathf.Cos(Time.time * 2f * Mathf.PI / period)) * 0.5f;
        Vector3 next = start + travel * t;
        Vector3 delta = next - transform.position;

        CharacterController player = ParkourCourse.Player;
        bool carry = player != null && player.enabled && top != null && IsStandingOn(player, top.bounds);

        // Move whichever is in front first so the platform never pushes into the player's feet
        if (carry && delta.y > 0f) player.Move(delta);
        transform.position = next;
        Physics.SyncTransforms();
        if (carry && delta.y <= 0f) player.Move(delta);
    }

    private static bool IsStandingOn(CharacterController player, Bounds surface)
    {
        Bounds feet = player.bounds;
        return feet.min.y > surface.max.y - 0.2f && feet.min.y < surface.max.y + 0.3f
            && feet.center.x > surface.min.x - 0.2f && feet.center.x < surface.max.x + 0.2f
            && feet.center.z > surface.min.z - 0.2f && feet.center.z < surface.max.z + 0.2f;
    }
}
