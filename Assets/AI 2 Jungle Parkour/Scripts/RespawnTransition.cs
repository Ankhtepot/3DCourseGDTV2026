using System.Collections;
using UnityEngine;

/// <summary>
/// Respawn effect. The character collapses into a beam of light and vanishes, the camera glides along an arc
/// to where it will frame her at the checkpoint (at a steady average speed), and she rebuilds from a ring of light.
/// Player input and the orbit camera are switched off for the whole transition.
/// </summary>
public class RespawnTransition : MonoBehaviour
{
    [SerializeField] private Material particleMaterial;
    [SerializeField, ColorUsage(false, true)] private Color effectColor = new Color(0.5f, 2.6f, 2.2f);

    [Header("Camera glide")]
    [Tooltip("Average camera speed on the way to the checkpoint, in m/s.")]
    [SerializeField] private float glideSpeed = 10f;
    [SerializeField] private float minGlideTime = 0.4f;
    [Tooltip("Camera pitch when she reappears.")]
    [SerializeField] private float respawnPitch = 15f;

    [Header("Timing")]
    [SerializeField] private float vanishTime = 0.35f;
    [SerializeField] private float appearTime = 0.45f;

    public bool IsRunning { get; private set; }

    /// <summary>Moves the player to a new pose with the full effect. A finite maxGlideTime speeds up long glides.</summary>
    public void Play(CharacterController player, Vector3 position, Quaternion rotation, float maxGlideTime = float.PositiveInfinity)
    {
        if (IsRunning || player == null) return;
        StartCoroutine(Run(player, position, rotation, maxGlideTime));
    }

    private IEnumerator Run(CharacterController player, Vector3 position, Quaternion rotation, float maxGlideTime)
    {
        IsRunning = true;
        var mover = player.GetComponent<ThirdPersonController>();
        var archer = player.GetComponent<ArcherController>();
        Camera view = Camera.main;
        ThirdPersonCamera orbit = view != null ? view.GetComponent<ThirdPersonCamera>() : null;
        Animator animator = player.GetComponentInChildren<Animator>();
        Transform model = animator != null ? animator.transform : player.transform;
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
        var wasVisible = new bool[renderers.Length];

        SetControl(player, mover, archer, orbit, false);

        // Vanish: squeeze into a thin column of light
        Bounds bounds = BodyBounds(renderers, player);
        SparkleBurst(bounds.center, bounds.size, 160);
        RisingStreaks(bounds.center - Vector3.up * bounds.extents.y, bounds.size.x * 0.4f, vanishTime + 0.2f, 120f);
        StartCoroutine(Flash(bounds.center, 0.5f));
        AudioPool.Play(SoundSynth.TeleportOut(), 0.6f);

        Vector3 restScale = model.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / vanishTime)
        {
            float e = t * t;
            model.localScale = Vector3.Scale(restScale, new Vector3(1f - e, 1f + e * 0.5f, 1f - e));
            yield return null;
        }
        for (int i = 0; i < renderers.Length; i++)
        {
            wasVisible[i] = renderers[i].enabled;
            renderers[i].enabled = false;
        }
        model.localScale = restScale;

        // Move her while she's invisible, then fly the camera to where it will frame her
        Vector3 fromFocus = bounds.center;
        player.transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        if (mover != null) mover.ResetMotion();

        if (orbit != null)
        {
            orbit.SetOrientation(rotation.eulerAngles.y, respawnPitch);
            orbit.GetPose(out Vector3 endPosition, out Quaternion endRotation);
            Vector3 toFocus = endPosition + endRotation * Vector3.forward * Vector3.Distance(endPosition, position + Vector3.up * 1.5f);
            yield return Glide(view.transform, endPosition, endRotation, fromFocus, toFocus, maxGlideTime);
        }

        // Appear: a ring of light at her feet and she grows back out of the beam
        RisingStreaks(position, 0.6f, appearTime, 260f);
        AudioPool.Play(SoundSynth.TeleportIn(), 0.6f);
        yield return new WaitForSeconds(appearTime * 0.4f);

        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = wasVisible[i];
        Bounds landed = BodyBounds(renderers, player);
        SparkleBurst(landed.center, landed.size, 90);
        StartCoroutine(Flash(landed.center, 0.4f));
        for (float t = 0f; t < 1f; t += Time.deltaTime / (appearTime * 0.6f))
        {
            float e = 1f - (1f - t) * (1f - t);
            model.localScale = Vector3.Scale(restScale, new Vector3(e, 1.5f - e * 0.5f, e));
            yield return null;
        }
        model.localScale = restScale;

        SetControl(player, mover, archer, orbit, true);
        if (mover != null) mover.ResetMotion();
        IsRunning = false;
    }

    private IEnumerator Glide(Transform cameraTransform, Vector3 toPosition, Quaternion toRotation, Vector3 fromFocus, Vector3 toFocus, float maxTime)
    {
        Vector3 fromPosition = cameraTransform.position;
        Quaternion fromRotation = cameraTransform.rotation;

        // Arc upwards so the camera clears the ravine walls and ruins on the way
        float straight = Vector3.Distance(fromPosition, toPosition);
        Vector3 control = (fromPosition + toPosition) * 0.5f + Vector3.up * Mathf.Clamp(straight * 0.3f, 0.5f, 10f);
        float length = 0f;
        Vector3 previous = fromPosition;
        for (int i = 1; i <= 24; i++)
        {
            Vector3 point = Bezier(fromPosition, control, toPosition, i / 24f);
            length += Vector3.Distance(previous, point);
            previous = point;
        }

        // Eased, so the speed averages out to glideSpeed
        float duration = Mathf.Clamp(length / glideSpeed, minGlideTime, Mathf.Max(maxTime, minGlideTime));
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            Vector3 position = Bezier(fromPosition, control, toPosition, e);
            Vector3 lookDirection = Vector3.Lerp(fromFocus, toFocus, e) - position;
            Quaternion look = lookDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDirection) : toRotation;

            // Ease out of the starting view, track the focus point, settle into the final framing
            Quaternion rotation = Quaternion.Slerp(fromRotation, look, Mathf.SmoothStep(0f, 1f, t / 0.25f));
            rotation = Quaternion.Slerp(rotation, toRotation, Mathf.SmoothStep(0f, 1f, (t - 0.7f) / 0.3f));
            cameraTransform.SetPositionAndRotation(position, rotation);
            yield return null;
        }
        cameraTransform.SetPositionAndRotation(toPosition, toRotation);
    }

    private static Vector3 Bezier(Vector3 a, Vector3 control, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * control + t * t * b;
    }

    private static void SetControl(CharacterController player, ThirdPersonController mover, ArcherController archer, ThirdPersonCamera orbit, bool on)
    {
        player.enabled = on;
        if (mover != null) mover.enabled = on;
        if (archer != null) archer.enabled = on;
        if (orbit != null) orbit.enabled = on;
    }

    private static Bounds BodyBounds(Renderer[] renderers, CharacterController player)
    {
        Bounds bounds = new Bounds(player.transform.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
        foreach (Renderer r in renderers)
            if (r.enabled && r is SkinnedMeshRenderer) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    // ---------------------------------------------------------------- Effects

    private void SparkleBurst(Vector3 center, Vector3 size, int count)
    {
        ParticleSystem particles = CreateParticles("Teleport Sparkles", center, Quaternion.identity);
        ParticleSystem.MainModule main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
        main.gravityModifier = -0.35f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = size * 0.8f;

        particles.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        particles.Play();
    }

    private void RisingStreaks(Vector3 floor, float radius, float duration, float rate)
    {
        ParticleSystem particles = CreateParticles("Teleport Beam", floor, Quaternion.Euler(-90f, 0f, 0f));
        ParticleSystem.MainModule main = particles.main;
        main.duration = duration;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 2f;
        shape.radius = radius;
        shape.radiusThickness = 0.3f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.12f;
        renderer.lengthScale = 2f;
        particles.Play();
    }

    private ParticleSystem CreateParticles(string name, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject(name);
        go.transform.SetPositionAndRotation(position, rotation);
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;
        main.startColor = new ParticleSystem.MinMaxGradient(effectColor, Color.Lerp(effectColor, Color.white * 3f, 0.6f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        ParticleSystem.SizeOverLifetimeModule shrink = particles.sizeOverLifetime;
        shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Destroy(go, 4f);
        return particles;
    }

    private IEnumerator Flash(Vector3 position, float duration)
    {
        var light = new GameObject("Teleport Flash").AddComponent<Light>();
        light.transform.position = position;
        light.type = LightType.Point;
        light.color = new Color(0.5f, 1f, 0.9f);
        light.range = 7f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            light.intensity = 8f * (1f - t) * (1f - t);
            yield return null;
        }
        Destroy(light.gameObject);
    }
}
