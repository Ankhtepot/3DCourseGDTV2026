using UnityEngine;

/// <summary>
/// A flying arrow. Moves (optionally under gravity), points along its velocity and sticks into whatever its tip hits.
/// Uses raycasts between frames instead of a Rigidbody so fast arrows never pass through thin objects.
/// The arrow's pivot is the nock, the tip is tipDistance along +Z.
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
    [Tooltip("0 = arrows fly straight.")]
    [SerializeField] private float gravityScale = 0f;
    [SerializeField] private float tipDistance = 0.835f;
    [Tooltip("How deep the tip ends up in the target.")]
    [SerializeField] private float penetration = 0.12f;
    [SerializeField] private float lifetime = 30f;
    [SerializeField] private float impactImpulse = 2f;

    private Vector3 velocity;
    private Transform owner;
    private bool stuck;

    public void Launch(Vector3 launchVelocity, Transform shooter)
    {
        velocity = launchVelocity;
        owner = shooter;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (stuck) return;

        Vector3 tip = transform.position + transform.forward * tipDistance;
        velocity += Physics.gravity * (gravityScale * Time.deltaTime);
        Vector3 step = velocity * Time.deltaTime;

        if (TryHit(tip, step, out RaycastHit hit))
        {
            Stick(hit);
            return;
        }

        transform.position += step;
        transform.rotation = Quaternion.LookRotation(velocity);
    }

    private bool TryHit(Vector3 from, Vector3 step, out RaycastHit closest)
    {
        closest = default;
        float best = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(from, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && hit.transform.IsChildOf(owner)) continue;
            if (hit.distance >= best) continue;
            best = hit.distance;
            closest = hit;
        }
        return best < float.MaxValue;
    }

    private void Stick(RaycastHit hit)
    {
        stuck = true;
        Vector3 direction = velocity.normalized;
        transform.SetPositionAndRotation(hit.point - direction * (tipDistance - penetration), Quaternion.LookRotation(direction));

        if (hit.rigidbody != null) hit.rigidbody.AddForceAtPosition(direction * impactImpulse, hit.point, ForceMode.Impulse);

        // Follow moving targets, but don't inherit a non-uniform scale
        Vector3 scale = hit.transform.lossyScale;
        if (Mathf.Approximately(scale.x, scale.y) && Mathf.Approximately(scale.y, scale.z)) transform.SetParent(hit.transform, true);
    }
}
