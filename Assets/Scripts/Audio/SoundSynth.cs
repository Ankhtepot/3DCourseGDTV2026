using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sound effects synthesised in code: chimes, footsteps, jumps, landings, teleports, bird calls and looping
/// jungle ambience. Each sound is generated on first use and cached; sounds with variants return a random one.
/// </summary>
public static class SoundSynth
{
    public const int SampleRate = 44100;

    private static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();

    public static AudioClip Coin() => Get("Coin", 1, _ => Chime(new[] { 988f, 1319f }, 0.07f, 0.3f));
    public static AudioClip Checkpoint() => Get("Checkpoint", 1, _ => Chime(new[] { 523f, 659f, 784f }, 0.1f, 0.4f));
    public static AudioClip Fanfare() => Get("Fanfare", 1, _ => Chime(new[] { 523f, 659f, 784f, 1047f, 784f, 1047f }, 0.13f, 0.7f));
    public static AudioClip StoneStep() => Get("Stone Step", 6, StoneStep);
    public static AudioClip SoftStep() => Get("Soft Step", 6, SoftStep);
    public static AudioClip Jump() => Get("Jump", 3, Jump);
    public static AudioClip Land() => Get("Land", 3, Land);
    public static AudioClip TeleportOut() => Get("Teleport Out", 1, TeleportOut);
    public static AudioClip TeleportIn() => Get("Teleport In", 1, TeleportIn);
    public static AudioClip BirdCall() => Get("Bird Call", 10, BirdCall);
    public static AudioClip WindLoop() => Get("Wind Loop", 1, _ => Loop(8f, WindLoop));
    public static AudioClip InsectLoop() => Get("Insect Loop", 1, _ => Loop(4f, InsectLoop));
    public static AudioClip RiverLoop() => Get("River Loop", 1, _ => Loop(4f, RiverLoop));
    public static AudioClip WaterfallLoop() => Get("Waterfall Loop", 1, _ => Loop(4f, WaterfallLoop));

    private static AudioClip Get(string name, int variants, System.Func<System.Random, float[]> make)
    {
        if (!cache.TryGetValue(name, out AudioClip[] clips) || clips[0] == null)
        {
            clips = new AudioClip[variants];
            for (int i = 0; i < variants; i++)
            {
                float[] data = make(new System.Random(name.Length * 7919 + name[0] * 31 + i));
                clips[i] = AudioClip.Create($"{name} {i + 1}", data.Length, 1, SampleRate, false);
                clips[i].SetData(data, 0);
            }
            cache[name] = clips;
        }
        return clips[Random.Range(0, clips.Length)];
    }

    // ---------------------------------------------------------------- One-shots

    /// <summary>Bell-like sine notes played one after another, each fading out.</summary>
    private static float[] Chime(float[] notes, float noteLength, float tail)
    {
        int noteSamples = Samples(noteLength);
        var data = new float[noteSamples * (notes.Length - 1) + Samples(tail)];
        for (int n = 0; n < notes.Length; n++)
        {
            for (int i = n * noteSamples; i < data.Length; i++)
            {
                float t = (i - n * noteSamples) / (float)SampleRate;
                float env = Mathf.Exp(-t * 9f) * Mathf.Clamp01(t / 0.003f);
                data[i] += (Mathf.Sin(2f * Mathf.PI * notes[n] * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * notes[n] * t)) * env;
            }
        }
        return Normalize(data, 0.8f);
    }

    /// <summary>Hard heel strike: a bright noise click over a short low knock.</summary>
    private static float[] StoneStep(System.Random r)
    {
        var data = new float[Samples(0.1f)];
        float a = Coef(Range(r, 1800f, 3200f));
        float knock = Range(r, 110f, 170f);
        float lp = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            lp += (Noise(r) - lp) * a;
            float env = Mathf.Exp(-t * 65f) * Mathf.Clamp01(t / 0.001f);
            data[i] = lp * env * 2.5f + Mathf.Sin(2f * Mathf.PI * knock * t) * Mathf.Exp(-t * 45f) * 0.6f;
        }
        return Normalize(data, 0.9f);
    }

    /// <summary>Foot on grass and earth: a muffled heel then toe, with a little rustle.</summary>
    private static float[] SoftStep(System.Random r)
    {
        var data = new float[Samples(0.18f)];
        float aLow = Coef(Range(r, 1100f, 1700f)), aHigh = Coef(220f);
        float toe = Range(r, 0.03f, 0.05f);
        float lp = 0f, hp = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            lp += (Noise(r) - lp) * aLow;
            hp += (lp - hp) * aHigh;
            float band = lp - hp;
            float env = Strike(t, 0.006f, 28f) + 0.6f * Strike(t - toe, 0.006f, 35f);
            data[i] = band * env;
        }
        return Normalize(data, 0.8f);
    }

    /// <summary>Cloth whoosh: noise through a filter that opens and closes.</summary>
    private static float[] Jump(System.Random r)
    {
        const float length = 0.26f;
        var data = new float[Samples(length)];
        float peak = Range(r, 2000f, 3200f);
        float lp = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float hump = Mathf.Sin(Mathf.PI * t / length);
            lp += (Noise(r) - lp) * Coef(400f + peak * hump);
            data[i] = lp * hump * hump;
        }
        return Normalize(data, 0.7f);
    }

    /// <summary>Landing thud: a falling low tone with a burst of dull noise.</summary>
    private static float[] Land(System.Random r)
    {
        var data = new float[Samples(0.4f)];
        float a = Coef(Range(r, 700f, 1000f));
        float lp = 0f, phase = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float frequency = Mathf.Lerp(95f, 45f, Mathf.Clamp01(t / 0.2f));
            phase += 2f * Mathf.PI * frequency / SampleRate;
            lp += (Noise(r) - lp) * a;
            data[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 13f) * Mathf.Clamp01(t / 0.002f) + lp * Mathf.Exp(-t * 22f) * 3f;
        }
        return Normalize(data, 0.9f);
    }

    /// <summary>Rising shimmer: three detuned voices sweep up three octaves over glittering pings.</summary>
    private static float[] TeleportOut(System.Random r)
    {
        const float length = 0.85f;
        var data = new float[Samples(length)];
        var phases = new float[3];
        float[] ratios = { 1f, 1.5f, 2.01f };
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate, u = t / length;
            float baseFrequency = 220f * Mathf.Pow(2f, 3f * u) * (1f + 0.01f * Mathf.Sin(2f * Mathf.PI * 7f * t));
            float voice = 0f;
            for (int v = 0; v < 3; v++)
            {
                phases[v] += 2f * Mathf.PI * baseFrequency * ratios[v] / SampleRate;
                voice += Mathf.Sin(phases[v]) / (v + 1f);
            }
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Pow(1f - u, 1.5f);
            data[i] = voice * env * 0.6f + Noise(r) * 0.08f * env;
        }
        AddPings(data, r, 18, 2000f, 5000f);
        return Normalize(data, 0.8f);
    }

    /// <summary>Falling shimmer that resolves into a bright chord.</summary>
    private static float[] TeleportIn(System.Random r)
    {
        const float sweep = 0.5f;
        var data = new float[Samples(1.1f)];
        var phases = new float[3];
        float[] ratios = { 1f, 1.5f, 2.01f };
        float[] chord = { 523f, 659f, 784f, 1047f };
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float value = 0f;
            if (t < sweep)
            {
                float u = t / sweep;
                float baseFrequency = 1760f * Mathf.Pow(2f, -2f * u);
                for (int v = 0; v < 3; v++)
                {
                    phases[v] += 2f * Mathf.PI * baseFrequency * ratios[v] / SampleRate;
                    value += Mathf.Sin(phases[v]) / (v + 1f);
                }
                value *= Mathf.Clamp01(t / 0.02f) * (1f - u * 0.5f) * 0.5f;
            }
            float ct = t - sweep * 0.85f;
            if (ct > 0f)
                foreach (float note in chord)
                    value += Mathf.Sin(2f * Mathf.PI * note * ct) * Mathf.Exp(-ct * 5f) * Mathf.Clamp01(ct / 0.004f) * 0.3f;
            data[i] = value;
        }
        AddPings(data, r, 12, 2500f, 6000f);
        return Normalize(data, 0.8f);
    }

    /// <summary>A few chirped syllables, each a quick pitch sweep, sometimes with a trill.</summary>
    private static float[] BirdCall(System.Random r)
    {
        int syllables = r.Next(2, 6);
        float start = Range(r, 2200f, 4200f);
        bool trill = r.NextDouble() < 0.35;
        var data = new float[Samples(0.9f)];
        int cursor = 0;
        float phase = 0f;
        for (int s = 0; s < syllables && cursor < data.Length; s++)
        {
            int length = Samples(Range(r, 0.05f, 0.14f));
            float from = start * Range(r, 0.85f, 1.15f), to = from * Range(r, 0.6f, 1.6f);
            for (int i = 0; i < length && cursor + i < data.Length; i++)
            {
                float u = i / (float)length, t = i / (float)SampleRate;
                float frequency = Mathf.Lerp(from, to, u) * (trill ? 1f + 0.08f * Mathf.Sin(2f * Mathf.PI * 45f * t) : 1f);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                float env = Mathf.Sin(Mathf.PI * u);
                data[cursor + i] = (Mathf.Sin(phase) + 0.2f * Mathf.Sin(2f * phase)) * env * env;
            }
            cursor += length + Samples(Range(r, 0.03f, 0.1f));
        }
        System.Array.Resize(ref data, Mathf.Min(cursor, data.Length));
        return Normalize(data, 0.7f);
    }

    // ---------------------------------------------------------------- Loops

    /// <summary>Low rushing air that swells in gusts, with leaves rustling on the gusts.</summary>
    private static float[] WindLoop(int samples, float loopSeconds, System.Random r)
    {
        var data = new float[samples];
        float aLow = Coef(350f), aLeaf = Coef(3000f), aLeafLow = Coef(800f);
        float low = 0f, leaf = 0f, leafLow = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float n = Noise(r);
            low += (n - low) * aLow;
            leaf += (n - leaf) * aLeaf;
            leafLow += (leaf - leafLow) * aLeafLow;
            float gust = 0.55f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / loopSeconds) + 0.2f * Mathf.Sin(2f * Mathf.PI * 3f * t / loopSeconds + 1f);
            data[i] = low * 3f * gust + (leaf - leafLow) * 0.5f * gust * gust;
        }
        return data;
    }

    /// <summary>Cicadas: high band of noise pulsing about thirty times a second.</summary>
    private static float[] InsectLoop(int samples, float loopSeconds, System.Random r)
    {
        var data = new float[samples];
        float aHigh = Coef(7500f), aLow = Coef(4200f);
        float high = 0f, low = 0f;
        float pulses = Mathf.Round(30f * loopSeconds);
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            high += (Noise(r) - high) * aHigh;
            low += (high - low) * aLow;
            float pulse = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * pulses * t / loopSeconds);
            float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / loopSeconds);
            data[i] = (high - low) * pulse * pulse * swell;
        }
        return data;
    }

    /// <summary>Running water: filtered noise with little bubbling blips.</summary>
    private static float[] RiverLoop(int samples, float loopSeconds, System.Random r)
    {
        var data = new float[samples];
        float aLow = Coef(900f), aHigh = Coef(120f);
        float low = 0f, high = 0f;
        for (int i = 0; i < samples; i++)
        {
            low += (Noise(r) - low) * aLow;
            high += (low - high) * aHigh;
            data[i] = (low - high) * 2f;
        }
        int bubbles = Mathf.RoundToInt(loopSeconds * 40f);
        for (int b = 0; b < bubbles; b++)
        {
            int start = r.Next(samples);
            int length = Samples(Range(r, 0.01f, 0.03f));
            float from = Range(r, 350f, 900f), phase = 0f;
            for (int i = 0; i < length && start + i < samples; i++)
            {
                float u = i / (float)length;
                phase += 2f * Mathf.PI * from * (1f + u) / SampleRate;
                data[start + i] += Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * u) * 0.15f;
            }
        }
        return data;
    }

    /// <summary>Falling water: broad noise over a low rumble.</summary>
    private static float[] WaterfallLoop(int samples, float loopSeconds, System.Random r)
    {
        var data = new float[samples];
        float aBright = Coef(5000f), aCut = Coef(200f), aRumble = Coef(300f);
        float bright = 0f, cut = 0f, rumble = 0f;
        for (int i = 0; i < samples; i++)
        {
            float n = Noise(r);
            bright += (n - bright) * aBright;
            cut += (bright - cut) * aCut;
            rumble += (n - rumble) * aRumble;
            data[i] = (bright - cut) + rumble * 2.5f;
        }
        return data;
    }

    /// <summary>Builds a seamless loop: renders a little extra and crossfades the overhang into the start.</summary>
    private static float[] Loop(float seconds, System.Func<int, float, System.Random, float[]> render)
    {
        int length = Samples(seconds), fade = Samples(0.25f);
        float[] raw = render(length + fade, seconds, new System.Random(Mathf.RoundToInt(seconds * 1000f) + render.Method.Name.Length));
        var data = new float[length];
        System.Array.Copy(raw, data, length);
        for (int i = 0; i < fade; i++)
        {
            float w = i / (float)fade;
            data[i] = raw[i] * w + raw[length + i] * (1f - w);
        }
        return Normalize(data, 0.8f);
    }

    // ---------------------------------------------------------------- Helpers

    private static void AddPings(float[] data, System.Random r, int count, float minFrequency, float maxFrequency)
    {
        for (int p = 0; p < count; p++)
        {
            int start = r.Next(data.Length);
            float frequency = Range(r, minFrequency, maxFrequency);
            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)SampleRate;
                if (t > 0.08f) break;
                data[i] += Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 45f) * 0.15f;
            }
        }
    }

    private static float Strike(float t, float attack, float decay) => t < 0f ? 0f : Mathf.Clamp01(t / attack) * Mathf.Exp(-t * decay);

    /// <summary>One-pole low-pass filter coefficient for a cutoff in Hz.</summary>
    private static float Coef(float cutoff) => 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);

    private static int Samples(float seconds) => Mathf.CeilToInt(seconds * SampleRate);

    private static float Noise(System.Random r) => (float)r.NextDouble() * 2f - 1f;

    private static float Range(System.Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);

    private static float[] Normalize(float[] data, float peak)
    {
        float max = 0f;
        foreach (float sample in data) max = Mathf.Max(max, Mathf.Abs(sample));
        if (max > 0f)
            for (int i = 0; i < data.Length; i++) data[i] *= peak / max;
        return data;
    }
}
