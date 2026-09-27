using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GodAmp.Audio.Playback;
using GodAmp.Autoload;
using GodAmp.Data;
using Godot;

namespace GodAmp.Audio.Processing;

/// <summary>Owns shared audio processing, effect validation, and persistence independently of its views.</summary>
public partial class AudioController : Node
{
    public const float MinimumGainDb = -12;
    public const float MaximumGainDb = 12;

    [Signal] public delegate void StateChangedEventHandler();
    [Export] private StringName _busName = "Master";
    [Export] private TrackPlayer _player = null!;

    private readonly float[] _bandGains = new float[AudioSettings.EqualizerBandCount];
    private readonly ReadOnlyCollection<float> _readOnlyGains;
    private AudioEffectAmplify _preamp = null!;
    private AudioEffectEQ10 _equalizer = null!;
    private AudioEffectPanner _panner = null!;
    private int _busIndex;
    private int _preampIndex;
    private int _equalizerIndex;

    public AudioController() => _readOnlyGains = Array.AsReadOnly(_bandGains);

    public float Volume { get; private set; }
    public float Balance { get; private set; }
    public float PreampDb { get; private set; }
    public bool EqualizerEnabled { get; private set; }
    public IReadOnlyList<float> BandGains => _readOnlyGains;
    public AudioEffectSpectrumAnalyzerInstance Spectrum { get; private set; } = null!;
    public AudioEffectCapture Capture { get; private set; } = null!;

    /// <summary>Resolves the scene's audio bus before views read state and restores validated preferences.</summary>
    public override void _Ready()
    {
        _busIndex = AudioServer.GetBusIndex(_busName);
        if (_busIndex < 0)
            throw new InvalidOperationException($"Audio bus '{_busName}' is missing.");
        (_preamp, _preampIndex) = FindEffect<AudioEffectAmplify>();
        (_equalizer, _equalizerIndex) = FindEffect<AudioEffectEQ10>();
        (_panner, _) = FindEffect<AudioEffectPanner>();
        (Capture, _) = FindEffect<AudioEffectCapture>();
        (_, int spectrumIndex) = FindEffect<AudioEffectSpectrumAnalyzer>();
        Spectrum = AudioServer.GetBusEffectInstance(_busIndex, spectrumIndex) as AudioEffectSpectrumAnalyzerInstance
            ?? throw new InvalidOperationException("The spectrum analyzer instance is unavailable.");
        if (_equalizer.GetBandCount() != AudioSettings.EqualizerBandCount)
            throw new InvalidOperationException("The audio bus requires a ten-band equalizer.");
        Restore(SettingsManager.Instance.GetAudioSettings());
    }

    private (T Effect, int Index) FindEffect<T>() where T : AudioEffect
    {
        T? effect = null;
        int index = -1;
        for (int i = 0; i < AudioServer.GetBusEffectCount(_busIndex); i++)
        {
            if (AudioServer.GetBusEffect(_busIndex, i) is not T candidate)
                continue;
            if (effect != null)
                throw new InvalidOperationException($"Audio bus '{_busName}' has multiple {typeof(T).Name} effects.");
            effect = candidate;
            index = i;
        }
        return effect != null ? (effect, index)
            : throw new InvalidOperationException($"Audio bus '{_busName}' requires {typeof(T).Name}.");
    }

    private void Restore(AudioSettings settings)
    {
        Volume = Bounded(settings.Volume, 0, 1, AudioSettings.DefaultVolume);
        Balance = Bounded(settings.Balance, -1, 1, 0);
        PreampDb = Bounded(settings.PreampDb, MinimumGainDb, MaximumGainDb, 0);
        EqualizerEnabled = settings.EqualizerEnabled;
        for (int i = 0; i < _bandGains.Length; i++)
            _bandGains[i] = i < settings.BandGains.Length ? Bounded(settings.BandGains[i], MinimumGainDb, MaximumGainDb, 0) : 0;
        Apply();
    }

    /// <summary>Sets linear output volume, ignoring nonfinite input and clamping to zero through one.</summary>
    /// <param name="value">Requested linear output level.</param>
    public void SetVolume(float value)
    {
        if (!float.IsFinite(value) || Volume == Math.Clamp(value, 0, 1)) return;
        Volume = Math.Clamp(value, 0, 1);
        Publish();
    }

    /// <summary>Sets stereo balance, clamping finite input from minus one (left) through one (right).</summary>
    /// <param name="value">Requested stereo pan.</param>
    public void SetBalance(float value)
    {
        if (!float.IsFinite(value) || Balance == Math.Clamp(value, -1, 1)) return;
        Balance = Math.Clamp(value, -1, 1);
        Publish();
    }

    /// <summary>Sets preamp gain in decibels within the classic EQ range.</summary>
    /// <param name="value">Requested gain; nonfinite input is ignored.</param>
    public void SetPreamp(float value)
    {
        if (!float.IsFinite(value) || PreampDb == Math.Clamp(value, MinimumGainDb, MaximumGainDb)) return;
        PreampDb = Math.Clamp(value, MinimumGainDb, MaximumGainDb);
        Publish();
    }

    /// <summary>Sets an EQ band without allowing a UI control to own the native effect.</summary>
    /// <param name="value">Finite gain in decibels, clamped to the classic EQ range.</param>
    /// <param name="band">Zero-based index in the ten-band EQ order.</param>
    public void SetBandGain(float value, int band)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(band);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(band, _bandGains.Length);
        if (!float.IsFinite(value) || _bandGains[band] == Math.Clamp(value, MinimumGainDb, MaximumGainDb)) return;
        _bandGains[band] = Math.Clamp(value, MinimumGainDb, MaximumGainDb);
        Publish();
    }

    /// <summary>Enables adjusted preamp and EQ effects; neutral effects remain bypassed.</summary>
    /// <param name="enabled">Whether stored gains should affect playback.</param>
    public void SetEqualizerEnabled(bool enabled)
    {
        if (EqualizerEnabled == enabled) return;
        EqualizerEnabled = enabled;
        Publish();
    }

    private void Publish()
    {
        Apply();
        SettingsManager.Instance.SetAudioSettings(new AudioSettings(Volume, Balance, PreampDb, EqualizerEnabled, [.. _bandGains]));
        EmitSignal(SignalName.StateChanged);
    }

    private void Apply()
    {
        _player.VolumeLinear = Volume;
        _panner.Pan = Balance;
        _preamp.VolumeDb = PreampDb;
        for (int i = 0; i < _bandGains.Length; i++)
            _equalizer.SetBandGainDb(i, _bandGains[i]);
        AudioServer.SetBusEffectEnabled(_busIndex, _preampIndex, EqualizerEnabled && !Mathf.IsZeroApprox(PreampDb));
        AudioServer.SetBusEffectEnabled(_busIndex, _equalizerIndex, EqualizerEnabled && _bandGains.Any(gain => !Mathf.IsZeroApprox(gain)));
    }

    private static float Bounded(float value, float minimum, float maximum, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
