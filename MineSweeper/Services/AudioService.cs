using System;
using System.IO;
using System.Media;

namespace BugsFinder.Services;

/// <summary>
/// Synthesises short PCM sound effects at startup and exposes one-call
/// play methods for each game event. All sounds are generated programmatically
/// (no external audio files required) and pre-loaded into <see cref="SoundPlayer"/>
/// instances so playback latency is near-zero.
/// </summary>
public sealed class AudioService : IDisposable
{
    private const int Rate = 44100; // samples / second

    // One pre-loaded player per sound effect
    private readonly SoundPlayer _reveal;
    private readonly SoundPlayer _floodReveal;
    private readonly SoundPlayer _explosion;
    private readonly SoundPlayer _victory;
    private readonly SoundPlayer _flag;
    private readonly SoundPlayer _secretFound;
    private readonly SoundPlayer _starUsed;
    private readonly SoundPlayer _challengeAlert;
    private readonly SoundPlayer _countdownTick;

    /// <summary>Builds and pre-loads every sound effect.</summary>
    public AudioService()
    {
        // Single cell safe reveal  — soft 880 Hz pop, 80 ms
        _reveal = Load(Sine(880, 0.08, amp: 0.50, decay: 40));

        // Flood-fill reveal        — C5 → E5 → G5 ascending arpeggio, ~240 ms
        _floodReveal = Load(Concat(
            Sine(523.25, 0.07, amp: 0.45, decay: 25),
            Sine(659.25, 0.07, amp: 0.45, decay: 25),
            Sine(783.99, 0.10, amp: 0.45, decay: 20)));

        // Mine explodes (loss)     — low sine + filtered noise rumble, 600 ms
        _explosion = Load(Rumble(lowHz: 75, sec: 0.60, amp: 0.85));

        // Win                      — C4–E4–G4–C5 jingle, ~680 ms
        _victory = Load(Concat(
            Sine(261.63, 0.14, amp: 0.48, decay: 8),
            Sine(329.63, 0.14, amp: 0.48, decay: 8),
            Sine(392.00, 0.14, amp: 0.48, decay: 8),
            Sine(523.25, 0.26, amp: 0.48, decay: 5)));

        // Flag toggled             — brief 700 Hz tick, 55 ms
        _flag = Load(Sine(700, 0.055, amp: 0.30, decay: 55));

        // Secret cell found        — "ting-ting": E6 then G6, 70 ms each, bright & crisp
        _secretFound = Load(Concat(
            Sine(1318.51, 0.07, amp: 0.55, decay: 18),
            Sine(1567.98, 0.10, amp: 0.55, decay: 15)));

        // Star used (shield)       — soft descending two-tone warning, 180 ms
        _starUsed = Load(Concat(
            Sine(600, 0.08, amp: 0.40, decay: 20),
            Sine(440, 0.10, amp: 0.35, decay: 18)));

        // Challenge starts         — E5→C5→A4 descending alarm, ~300 ms
        _challengeAlert = Load(Concat(
            Sine(659.25, 0.08, amp: 0.55, decay: 15),
            Sine(523.25, 0.08, amp: 0.55, decay: 15),
            Sine(440.00, 0.12, amp: 0.50, decay: 12)));

        // Countdown tick           — punchy dual-tone alarm: 880 Hz + 1320 Hz, 180 ms
        _countdownTick = Load(Concat(
            Mix(Sine(880,  0.09, amp: 0.70, decay: 18),
                Sine(1320, 0.09, amp: 0.55, decay: 22)),
            Sine(660, 0.07, amp: 0.40, decay: 30)));
    }

    // ── Public playback API ───────────────────────────────────────────────────

    /// <summary>Plays a short pop for revealing a single safe cell.</summary>
    public void PlayReveal()      => SafePlay(_reveal);

    /// <summary>Plays an ascending arpeggio when flood-fill reveals many cells.</summary>
    public void PlayFloodReveal() => SafePlay(_floodReveal);

    /// <summary>Plays a low-frequency explosion rumble when the player hits a mine.</summary>
    public void PlayExplosion()   => SafePlay(_explosion);

    /// <summary>Plays a four-note ascending jingle on winning the game.</summary>
    public void PlayVictory()     => SafePlay(_victory);

    /// <summary>Plays a soft tick when the player places or removes a flag.</summary>
    public void PlayFlag()        => SafePlay(_flag);

    /// <summary>Plays a bright "ting-ting" when a secret bonus cell is discovered.</summary>
    public void PlaySecretFound() => SafePlay(_secretFound);

    /// <summary>Plays a soft descending warning when a star is consumed to block a bug hit.</summary>
    public void PlayStarUsed()        => SafePlay(_starUsed);

    /// <summary>Plays a descending three-note alarm when a challenge period starts.</summary>
    public void PlayChallengeAlert()  => SafePlay(_challengeAlert);

    /// <summary>Plays a sharp tick for each countdown second during a challenge.</summary>
    public void PlayCountdownTick()   => SafePlay(_countdownTick);

    /// <inheritdoc/>
    public void Dispose()
    {
        _reveal.Dispose();
        _floodReveal.Dispose();
        _explosion.Dispose();
        _victory.Dispose();
        _flag.Dispose();
        _secretFound.Dispose();
        _starUsed.Dispose();
        _challengeAlert.Dispose();
        _countdownTick.Dispose();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void SafePlay(SoundPlayer player)
    {
        try { player.Play(); } // non-blocking; silently ignored if no audio device
        catch { }
    }

    /// <summary>Wraps PCM samples in a WAV container and returns a pre-loaded player.</summary>
    private static SoundPlayer Load(short[] samples)
    {
        var ms     = ToWavStream(samples);
        var player = new SoundPlayer(ms);
        try { player.Load(); } catch { }
        return player;
    }

    // ── Synthesis primitives ──────────────────────────────────────────────────

    /// <summary>Generates a sine-wave tone with optional exponential decay.</summary>
    /// <param name="hz">Frequency in Hertz.</param>
    /// <param name="sec">Duration in seconds.</param>
    /// <param name="amp">Peak amplitude in the range (0, 1].</param>
    /// <param name="decay">Exponential decay rate (higher = shorter).</param>
    private static short[] Sine(double hz, double sec, double amp, double decay)
    {
        int n   = (int)(Rate * sec);
        var buf = new short[n];
        for (int i = 0; i < n; i++)
        {
            double t      = (double)i / Rate;
            double fadeIn = Math.Min(1.0, t / 0.004); // 4 ms de-click ramp
            double env    = decay > 0 ? Math.Exp(-decay * t) : 1.0;
            double v      = amp * fadeIn * env * Math.Sin(2 * Math.PI * hz * t);
            buf[i] = Pack(v);
        }
        return buf;
    }

    /// <summary>
    /// Generates an explosion-like rumble: low-frequency sine mixed with
    /// low-pass-filtered white noise, fading out over <paramref name="sec"/> seconds.
    /// </summary>
    private static short[] Rumble(double lowHz, double sec, double amp)
    {
        int n     = (int)(Rate * sec);
        var buf   = new short[n];
        var rng   = new Random(42); // fixed seed for deterministic playback
        double smooth = 0;
        for (int i = 0; i < n; i++)
        {
            double t     = (double)i / Rate;
            double white = rng.NextDouble() * 2 - 1;
            smooth = 0.85 * smooth + 0.15 * white; // first-order IIR low-pass
            double env   = Math.Exp(-4.5 * t);
            double v     = (0.55 * Math.Sin(2 * Math.PI * lowHz * t) + 0.45 * smooth)
                           * env * amp;
            buf[i] = Pack(v);
        }
        return buf;
    }

    /// <summary>
    /// Mixes two equal-length sample arrays by summing and clamping, simulating
    /// playing both tones simultaneously.
    /// </summary>
    private static short[] Mix(short[] a, short[] b)
    {
        int n   = Math.Min(a.Length, b.Length);
        var out_ = new short[n];
        for (int i = 0; i < n; i++)
            out_[i] = Pack((a[i] + b[i]) / 32767.0);
        return out_;
    }

    /// <summary>Concatenates multiple sample arrays end-to-end.</summary>
    private static short[] Concat(params short[][] parts)
    {
        int total = 0;
        foreach (var p in parts) total += p.Length;
        var result = new short[total];
        int pos    = 0;
        foreach (var p in parts) { p.CopyTo(result, pos); pos += p.Length; }
        return result;
    }

    /// <summary>Clamps a [-1, 1] double to a 16-bit signed PCM sample.</summary>
    private static short Pack(double v)
        => (short)Math.Clamp((int)(v * 32767), short.MinValue, short.MaxValue);

    // ── WAV serialisation ─────────────────────────────────────────────────────

    /// <summary>
    /// Wraps mono 16-bit PCM samples in a standard RIFF/WAV container
    /// and returns a <see cref="MemoryStream"/> positioned at offset 0.
    /// </summary>
    private static MemoryStream ToWavStream(short[] samples)
    {
        int dataBytes = samples.Length * sizeof(short);
        var ms        = new MemoryStream(44 + dataBytes);

        using var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true);

        // Helper: write a 4-character ASCII chunk identifier
        static void Id(BinaryWriter bw, string s)
        {
            foreach (char c in s) bw.Write((byte)c);
        }

        // RIFF header
        Id(w, "RIFF"); w.Write(36 + dataBytes); Id(w, "WAVE");

        // fmt sub-chunk (PCM, mono, 16-bit)
        Id(w, "fmt "); w.Write(16);
        w.Write((short)1);          // audio format: PCM
        w.Write((short)1);          // channels: mono
        w.Write(Rate);              // sample rate
        w.Write(Rate * 2);          // byte rate = Rate × channels × (bits/8)
        w.Write((short)2);          // block align = channels × (bits/8)
        w.Write((short)16);         // bits per sample

        // data sub-chunk
        Id(w, "data"); w.Write(dataBytes);
        foreach (var s in samples) w.Write(s);

        ms.Position = 0;
        return ms;
    }
}
