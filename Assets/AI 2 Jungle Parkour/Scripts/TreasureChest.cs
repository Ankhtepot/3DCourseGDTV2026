using System;
using DefaultNamespace;
using UnityEngine;

/// <summary>
/// Treasure chest necessary to finish the course. They need to be all collected in order to finish the course. The chest lid opens when the player reaches it.
/// </summary>
public class TreasureChest : MonoBehaviour
{
    [SerializeField] private float radius = 2f;
    [SerializeField] private Transform lid;
    [SerializeField] private float openAngle = -110f;
    [SerializeField] private float openSpeed = 3f;
    [Tooltip("Light over the treasure. It stays off while the lid is shut (it has no shadows, so it would shine through the chest) and fades in as the lid opens.")]
    [SerializeField] private Light glow;

    private bool reached;
    private float open;
    private float glowIntensity;

    private void Awake()
    {
        lid.localRotation = Quaternion.identity;
        if (glow)
        {
            glowIntensity = glow.intensity;
            SetGlow(0f);
        }
    }

    public void ResetGoal()
    {
        reached = false;
        open = 0f;
        if (lid) lid.localRotation = Quaternion.identity;
        SetGlow(0f);
    }

    private void Update()
    {
        CharacterController player = ParkourCourse.Player;
        if (!reached && player && player.enabled && player.bounds.SqrDistance(transform.position) < radius * radius)
        {
            reached = true;
            EventManager.TriggerOnPlayerReachedCourseObjective();
        }

        if (reached && lid && open < 1f)
        {
            open = Mathf.MoveTowards(open, 1f, openSpeed * Time.deltaTime);
            lid.localRotation = Quaternion.Euler(Mathf.SmoothStep(0f, openAngle, open), 0f, 0f);
            SetGlow(open);
        }
    }

    private void SetGlow(float amount)
    {
        if (!glow) return;
        glow.intensity = glowIntensity * amount;
        glow.enabled = amount > 0f;
    }
}
