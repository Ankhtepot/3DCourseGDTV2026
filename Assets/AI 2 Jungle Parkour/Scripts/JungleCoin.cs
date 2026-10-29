using UnityEngine;

/// <summary>
/// Spinning, bobbing coin that the player picks up by touching it. Collected coins pop up, shrink and
/// switch themselves off; ParkourCourse switches them back on when the course restarts.
/// </summary>
public class JungleCoin : MonoBehaviour
{
    private const float CollectDuration = 0.3f;

    [SerializeField] private float spinSpeed = 160f;
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 2.2f;
    [Tooltip("How close the player's collider has to get to pick the coin up.")]
    [SerializeField] private float pickupDistance = 0.4f;

    private Vector3 home;
    private Vector3 baseScale;
    private float phase;
    private float collectedAt = -1f;

    private void Awake()
    {
        home = transform.position;
        baseScale = transform.localScale;
        phase = Random.value * Mathf.PI * 2f;
    }

    private void OnEnable()
    {
        collectedAt = -1f;
        transform.position = home;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        if (collectedAt >= 0f)
        {
            float t = (Time.time - collectedAt) / CollectDuration;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }
            transform.position += Vector3.up * (4f * Time.deltaTime);
            transform.localScale = baseScale * (1f - t);
            transform.Rotate(0f, spinSpeed * 4f * Time.deltaTime, 0f, Space.World);
            return;
        }

        transform.position = home + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight);
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        CharacterController player = ParkourCourse.Player;
        if (player != null && player.enabled && player.bounds.SqrDistance(transform.position) < pickupDistance * pickupDistance)
        {
            collectedAt = Time.time;
            ParkourCourse.Instance.CollectCoin(this);
        }
    }
}
