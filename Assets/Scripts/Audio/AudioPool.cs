using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays sounds through a pool of AudioSources. A sound takes the first source that isn't playing;
/// when every source is busy a new one is created, so overlapping sounds never cut each other off.
/// The pool creates itself on first use.
/// </summary>
public class AudioPool : MonoBehaviour
{
    // Safety limit; past it the source closest to finishing is reused instead
    private const int MaxSources = 64;

    private static AudioPool instance;

    private readonly List<AudioSource> sources = new List<AudioSource>();

    private static AudioPool Instance
    {
        get
        {
            if (instance == null) instance = new GameObject("Audio Pool").AddComponent<AudioPool>();
            return instance;
        }
    }

    public int SourceCount => sources.Count;

    /// <summary>Plays a non-positional (2D) sound.</summary>
    public static AudioSource Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        => Instance.Start(clip, volume, pitch, null, 1f, 50f, false);

    /// <summary>Plays a sound at a point in the world.</summary>
    public static AudioSource PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float minDistance = 2f, float maxDistance = 40f)
        => Instance.Start(clip, volume, pitch, position, minDistance, maxDistance, false);

    /// <summary>Starts a looping sound, 2D when no position is given. Stop it with source.Stop().</summary>
    public static AudioSource PlayLoop(AudioClip clip, float volume = 1f, Vector3? position = null, float minDistance = 2f, float maxDistance = 40f)
        => Instance.Start(clip, volume, 1f, position, minDistance, maxDistance, true);

    private AudioSource Start(AudioClip clip, float volume, float pitch, Vector3? position, float minDistance, float maxDistance, bool loop)
    {
        if (clip == null) return null;

        AudioSource source = GetFreeSource();
        source.transform.position = position ?? transform.position;
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.loop = loop;
        source.spatialBlend = position.HasValue ? 1f : 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        source.Play();
        return source;
    }

    private AudioSource GetFreeSource()
    {
        foreach (AudioSource source in sources)
            if (source != null && !source.isPlaying) return source;

        if (sources.Count >= MaxSources) return SourceClosestToFinishing();

        var child = new GameObject($"Audio Source {sources.Count + 1}");
        child.transform.SetParent(transform);
        var created = child.AddComponent<AudioSource>();
        created.playOnAwake = false;
        sources.Add(created);
        return created;
    }

    private AudioSource SourceClosestToFinishing()
    {
        AudioSource best = null;
        float bestRemaining = float.MaxValue;
        foreach (AudioSource source in sources)
        {
            if (source.loop) continue;
            float remaining = (source.clip.length - source.time) / Mathf.Max(Mathf.Abs(source.pitch), 0.01f);
            if (remaining < bestRemaining)
            {
                bestRemaining = remaining;
                best = source;
            }
        }
        return best != null ? best : sources[0];
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
