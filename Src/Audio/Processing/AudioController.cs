using System;
using System.Collections.Generic;
using System.Linq;
using GodAmp.Settings;
using Godot;

namespace GodAmp.Audio.Processing;

/// <summary>Applies shared audio state to bus effects and publishes changes after updating preferences.</summary>
public partial class AudioController : Node
{
    public const float MinimumGainDb = AudioState.MinimumGainDb;
    public const float MaximumGainDb = AudioState.MaximumGainDb;

    [Signal] public delegate void StateChangedEventHandler();
    [Export] private StringName _busName = "Master";
    [Export] private TrackPlayer _player = null!;

    private readonly AudioState _state = new();
    private AudioEffectAmplify _preamp = null!;
    private AudioEffectEQ10 _equalizer = null!;
    private AudioEffectPanner _panner = null!;
    private int _busIndex;
    private int _preampIndex;
    private int _equalizerIndex;

    public float Volume => _state.Volume;
    public float Balance => _state.Balance;
    public float PreampDb => _state.PreampDb;
    public bool EqualizerEnabled => _state.EqualizerEnabled;
    public IReadOnlyList<float> BandGains => _state.BandGains;
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
        _state.Restore(SettingsManager.Instance.GetAudioSettings());
        Apply();
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

    /// <summary>Sets linear output volume, ignoring nonfinite input and clamping to zero through one.</summary>
    /// <param name="value">Requested linear output level.</param>
    public void SetVolume(float value)
    {
        if (_state.SetVolume(value)) Publish();
    }

    /// <summary>Sets stereo balance, clamping finite input from minus one (left) through one (right).</summary>
    /// <param name="value">Requested stereo pan.</param>
    public void SetBalance(float value)
    {
        if (_state.SetBalance(value)) Publish();
    }

    /// <summary>Sets preamp gain in decibels within the classic EQ range.</summary>
    /// <param name="value">Requested gain; nonfinite input is ignored.</param>
    public void SetPreamp(float value)
    {
        if (_state.SetPreamp(value)) Publish();
    }

    /// <summary>Sets an EQ band without allowing a UI control to own the native effect.</summary>
    /// <param name="value">Finite gain in decibels, clamped to the classic EQ range.</param>
    /// <param name="band">Zero-based index in the ten-band EQ order.</param>
    public void SetBandGain(float value, int band)
    {
        if (_state.SetBandGain(value, band)) Publish();
    }

    /// <summary>Enables adjusted preamp and EQ effects; neutral effects remain bypassed.</summary>
    /// <param name="enabled">Whether stored gains should affect playback.</param>
    public void SetEqualizerEnabled(bool enabled)
    {
        if (_state.SetEqualizerEnabled(enabled)) Publish();
    }

    private void Publish()
    {
        Apply();
        SettingsManager.Instance.SetAudioSettings(_state.Snapshot());
        EmitSignal(SignalName.StateChanged);
    }

    private void Apply()
    {
        _player.VolumeLinear = Volume;
        _panner.Pan = Balance;
        _preamp.VolumeDb = PreampDb;
        for (int i = 0; i < BandGains.Count; i++)
            _equalizer.SetBandGainDb(i, BandGains[i]);
        AudioServer.SetBusEffectEnabled(_busIndex, _preampIndex, EqualizerEnabled && !Mathf.IsZeroApprox(PreampDb));
        AudioServer.SetBusEffectEnabled(_busIndex, _equalizerIndex, EqualizerEnabled && BandGains.Any(gain => !Mathf.IsZeroApprox(gain)));
    }

}
