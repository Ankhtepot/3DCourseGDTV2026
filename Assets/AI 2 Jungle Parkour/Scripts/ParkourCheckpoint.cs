using UnityEngine;

/// <summary>
/// Respawn point on the course. Standing on it makes it the active checkpoint; falling below
/// its kill height puts the player back here, facing the way the checkpoint faces.
/// </summary>
public class ParkourCheckpoint : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Tooltip("Falling below this world height respawns the player here.")]
    [SerializeField] private float killY = 3f;
    [SerializeField] private float radius = 1.5f;
    [SerializeField] private Renderer rune;
    [SerializeField, ColorUsage(false, true)] private Color idleColor = new Color(0.1f, 0.35f, 0.3f);
    [SerializeField, ColorUsage(false, true)] private Color litColor = new Color(0.4f, 3f, 2.2f);

    private Material runeMaterial;

    public float KillY => killY;

    private void Update()
    {
        CharacterController player = ParkourCourse.Player;
        if (player == null || !player.enabled || ParkourCourse.Instance == null) return;

        Bounds bounds = player.bounds;
        Vector3 offset = bounds.center - transform.position;
        float feetAbove = bounds.min.y - transform.position.y;
        offset.y = 0f;
        if (offset.sqrMagnitude < radius * radius && feetAbove > -0.3f && feetAbove < 1f)
            ParkourCourse.Instance.ReachCheckpoint(this);
    }

    public void SetLit(bool lit)
    {
        if (rune == null) return;
        // An own material per rune rather than a property block, which the GPU Resident Drawer
        // can apply to other renderers sharing the mesh
        if (runeMaterial == null) runeMaterial = rune.material;
        runeMaterial.SetColor(EmissionColorId, lit ? litColor : idleColor);
    }

    private void OnDestroy()
    {
        if (runeMaterial != null) Destroy(runeMaterial);
    }
}
