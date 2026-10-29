using UnityEngine;

/// <summary>Makes a light flicker like a flame.</summary>
[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    [SerializeField] private float speed = 7f;
    [SerializeField, Range(0f, 1f)] private float amount = 0.3f;

    private Light torchLight;
    private float baseIntensity;
    private float seed;

    private void Awake()
    {
        torchLight = GetComponent<Light>();
        baseIntensity = torchLight.intensity;
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(seed, Time.time * speed);
        torchLight.intensity = baseIntensity * (1f - amount + noise * amount * 2f);
    }
}
