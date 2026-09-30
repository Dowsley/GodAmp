using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GodAmp.Audio.Processing;

/// <summary>Owns validated audio preferences independently of effects, controls and persistence.</summary>
public sealed class AudioState
{
    /// <summary>Lowest supported preamp and equalizer gain in decibels.</summary>
    public const float MinimumGainDb = -12;
    /// <summary>Highest supported preamp and equalizer gain in decibels.</summary>
    public const float MaximumGainDb = 12;

    private const float MinimumVolumeDb = -60;

    private readonly float[] _bandGains = new float[AudioSettings.EqualizerBandCount];
    private readonly ReadOnlyCollection<float> _readOnlyGains;
    private float _volume = AudioSettings.DefaultVolume;
    private float _balance;
    private float _preampDb;

    /// <summary>Creates neutral processing state at the default output volume.</summary>
    public AudioState() => _readOnlyGains = Array.AsReadOnly(_bandGains);

    /// <summary>Normalized volume slider position between zero and one.</summary>
    public float Volume => _volume;
    /// <summary>Linear amplitude gain for the 60 dB slider range, with exact silence at zero.</summary>
    public float OutputGain => Volume == 0 ? 0 : MathF.Pow(10, MinimumVolumeDb * (1 - Volume) / 20);
    /// <summary>Stereo pan between minus one (left) and one (right).</summary>
    public float Balance => _balance;
    /// <summary>Preamp gain in decibels.</summary>
    public float PreampDb => _preampDb;
    /// <summary>Whether stored preamp and band gains are enabled.</summary>
    public bool EqualizerEnabled { get; private set; }
    /// <summary>Read-only gains in native ten-band order.</summary>
    public IReadOnlyList<float> BandGains => _readOnlyGains;

    /// <summary>Restores preferences, bounding finite values and replacing nonfinite or missing gains.</summary>
    /// <param name="settings">Persisted snapshot; band arrays are copied into owned state.</param>
    public void Restore(AudioSettings settings)
    {
        _volume = Bounded(settings.Volume, 0, 1, AudioSettings.DefaultVolume);
        _balance = Bounded(settings.Balance, -1, 1, 0);
        _preampDb = Bounded(settings.PreampDb, MinimumGainDb, MaximumGainDb, 0);
        EqualizerEnabled = settings.EqualizerEnabled;
        for (int i = 0; i < _bandGains.Length; i++)
            _bandGains[i] = i < settings.BandGains.Length ? Bounded(settings.BandGains[i], MinimumGainDb, MaximumGainDb, 0) : 0;
    }

    /// <summary>Copies preferences for persistence without exposing owned gain storage.</summary>
    /// <returns>A detached snapshot in native ten-band order.</returns>
    public AudioSettings Snapshot() => new(Volume, Balance, PreampDb, EqualizerEnabled, [.. _bandGains]);

    /// <summary>Updates output level, ignoring nonfinite values and bounding finite input.</summary>
    /// <param name="value">Requested normalized volume slider position.</param>
    /// <returns>Whether the effective volume changed.</returns>
    public bool SetVolume(float value) => SetBounded(ref _volume, value, 0, 1);

    /// <summary>Updates stereo pan, ignoring nonfinite values and bounding finite input.</summary>
    /// <param name="value">Requested pan from minus one through one.</param>
    /// <returns>Whether the effective balance changed.</returns>
    public bool SetBalance(float value) => SetBounded(ref _balance, value, -1, 1);

    /// <summary>Updates preamp gain within the supported decibel range.</summary>
    /// <param name="value">Requested gain; nonfinite input is ignored.</param>
    /// <returns>Whether the effective preamp gain changed.</returns>
    public bool SetPreamp(float value) => SetBounded(ref _preampDb, value, MinimumGainDb, MaximumGainDb);

    /// <summary>Updates one band within the supported decibel range.</summary>
    /// <param name="value">Requested gain; nonfinite input is ignored.</param>
    /// <param name="band">Zero-based band index.</param>
    /// <returns>Whether the effective band gain changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The band index is outside the ten-band layout.</exception>
    public bool SetBandGain(float value, int band)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(band);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(band, _bandGains.Length);
        return SetBounded(ref _bandGains[band], value, MinimumGainDb, MaximumGainDb);
    }

    /// <summary>Changes whether preamp and equalizer processing is requested.</summary>
    /// <param name="enabled">Requested enabled state.</param>
    /// <returns>Whether the state changed.</returns>
    public bool SetEqualizerEnabled(bool enabled)
    {
        if (EqualizerEnabled == enabled) return false;
        EqualizerEnabled = enabled;
        return true;
    }

    private static bool SetBounded(ref float current, float requested, float minimum, float maximum)
    {
        if (!float.IsFinite(requested)) return false;
        float bounded = Math.Clamp(requested, minimum, maximum);
        if (current == bounded) return false;
        current = bounded;
        return true;
    }

    private static float Bounded(float value, float minimum, float maximum, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
