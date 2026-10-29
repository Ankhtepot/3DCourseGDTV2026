using UnityEngine;

/// <summary>
/// Treasure chest at the end of the course. Reaching it finishes the run and swings the lid open.
/// </summary>
public class TreasureGoal : MonoBehaviour
{
    [SerializeField] private float radius = 2f;
    [SerializeField] private Transform lid;
    [SerializeField] private float openAngle = -110f;
    [SerializeField] private float openSpeed = 3f;

    private bool reached;
    private float open;

    public void ResetGoal()
    {
        reached = false;
        open = 0f;
        if (lid != null) lid.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        CharacterController player = ParkourCourse.Player;
        if (!reached && player != null && player.enabled && player.bounds.SqrDistance(transform.position) < radius * radius)
        {
            reached = true;
            ParkourCourse.Instance.Finish();
        }

        if (reached && lid != null && open < 1f)
        {
            open = Mathf.MoveTowards(open, 1f, openSpeed * Time.deltaTime);
            lid.localRotation = Quaternion.Euler(Mathf.SmoothStep(0f, openAngle, open), 0f, 0f);
        }
    }
}
