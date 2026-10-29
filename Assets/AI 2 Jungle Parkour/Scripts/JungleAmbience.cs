using UnityEngine;

/// <summary>
/// Jungle soundscape, all synthesised: wind in the canopy and cicadas everywhere, the river along its course,
/// the roar of the waterfall, and birds calling from the trees around the listener.
/// </summary>
public class JungleAmbience : MonoBehaviour
{
    [SerializeField] private Transform waterfall;
    [Tooltip("Points along the river, each gets a looping water sound.")]
    [SerializeField] private Vector3[] riverPoints;

    [Header("Volumes")]
    [SerializeField] private float windVolume = 0.3f;
    [SerializeField] private float insectVolume = 0.05f;
    [SerializeField] private float riverVolume = 0.35f;
    [SerializeField] private float waterfallVolume = 0.9f;
    [SerializeField] private float birdVolume = 0.35f;

    [Header("Birds")]
    [SerializeField] private Vector2 birdInterval = new Vector2(1.5f, 6f);
    [SerializeField] private Vector2 birdDistance = new Vector2(10f, 35f);

    private float nextBird;

    private void Start()
    {
        AudioPool.PlayLoop(SoundSynth.WindLoop(), windVolume);
        AudioPool.PlayLoop(SoundSynth.InsectLoop(), insectVolume);
        if (waterfall != null) AudioPool.PlayLoop(SoundSynth.WaterfallLoop(), waterfallVolume, waterfall.position, 6f, 80f);
        if (riverPoints != null)
            foreach (Vector3 point in riverPoints) AudioPool.PlayLoop(SoundSynth.RiverLoop(), riverVolume, point, 3f, 25f);
        nextBird = Time.time + Random.Range(birdInterval.x, birdInterval.y);
    }

    private void Update()
    {
        if (Time.time < nextBird) return;
        nextBird = Time.time + Random.Range(birdInterval.x, birdInterval.y);

        Camera listener = Camera.main;
        if (listener == null) return;
        Vector2 direction = Random.insideUnitCircle.normalized * Random.Range(birdDistance.x, birdDistance.y);
        Vector3 position = listener.transform.position + new Vector3(direction.x, Random.Range(6f, 14f), direction.y);
        AudioPool.PlayAt(SoundSynth.BirdCall(), position, birdVolume * Random.Range(0.6f, 1f), Random.Range(0.85f, 1.15f), 4f, 60f);
    }
}
