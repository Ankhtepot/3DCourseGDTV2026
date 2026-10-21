using DefaultNamespace;
using UnityEngine;

public class ProximityHandler : MonoBehaviour
{
    private static readonly int Intensity = Shader.PropertyToID("_Intensity");

    [SerializeField] private CapsuleCollider touchCollider;
    [SerializeField] private GameObject proximityIndicator;

    private readonly Collider[] hits = new Collider[16];
    private int obstacleMask;
    private Transform playerTransform;
    private bool isFunctional = true;
    private Material proximityMaterial;

    private void Awake()
    {
        //Check if proximityIndicator is assigned and has Intensity property
        if (proximityIndicator != null
            && proximityIndicator.TryGetComponent<Renderer>(out var renderer)
            && renderer.material.HasProperty(Intensity))
        {
            proximityMaterial = renderer.material;
            isFunctional = true;
        }
        else
        {
            Debug.LogError("Proximity material is not assigned or does not have an Intensity property.");
            isFunctional = false;
        }

        if (touchCollider == null)
        {
            Debug.LogError("Touch collider is not assigned.");
            isFunctional = false;
        }

        obstacleMask = LayerMask.GetMask("Obstacle");
        if (obstacleMask == 0)
        {
            Debug.LogError("Layer 'Obstacle' does not exist.");
            isFunctional = false;
        }

        playerTransform = transform;
    }

    void Update()
    {
        if (!isFunctional) return;

        SetProximityIndicatorIntensity();
    }

    private void SetProximityIndicatorIntensity()
    {
        if (!isFunctional) return;

        ColliderHelper.GetCapsuleWorldPoints(touchCollider, out Vector3 point0, out Vector3 point1, out float radius);
        int count = Physics.OverlapCapsuleNonAlloc(point0, point1, radius, hits, obstacleMask,
            QueryTriggerInteraction.Collide);

        // Closest obstacle surface to the player, 0..1 (1 = touching the player, 0 = at the edge of touchCollider)
        float intensity = 0f;
        Vector3 playerPosition = playerTransform.position;
        for (int i = 0; i < count; i++)
        {
            Vector3 closestPoint = ColliderHelper.ClosestPointOn(hits[i], playerPosition);
            float distance = Vector3.Distance(closestPoint, playerPosition);
            intensity = Mathf.Max(intensity, 1f - Mathf.Clamp01(distance / radius));
        }
        
        if (proximityMaterial)
        {
            proximityMaterial.SetFloat(Intensity, intensity);
        }
    }
}
