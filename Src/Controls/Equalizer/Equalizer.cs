using GodAmp.Components;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.Equalizer;

/// <summary>Applies the skinned equalizer controls to the master audio bus.</summary>
public partial class Equalizer : WindowPanelContainer
{
    [ExportGroup("References")]
    [Export] private TextureButton _equalizerToggleButton = null!;
    [Export] private EqualizerGraph _graph = null!;
    private int _masterBusIndex;
    private AudioEffectAmplify _preamp = null!;
    private AudioEffectEQ10 _equalizer = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        base._Ready();
        _masterBusIndex = AudioServer.GetBusIndex("Master");
        _preamp = (AudioEffectAmplify)AudioServer.GetBusEffect(_masterBusIndex, AudioUtils.AmplifyAudioEffectIndex);
        _equalizer = (AudioEffectEQ10)AudioServer.GetBusEffect(_masterBusIndex, AudioUtils.Eq10AudioEffectIndex);
        RefreshAudioEffects();
    }

    /// <summary>Bypasses neutral effects so enabling flat EQ preserves the unprocessed signal.</summary>
    private void RefreshAudioEffects()
    {
        bool adjusted = false;
        for (int band = 0; band < _equalizer.GetBandCount(); band++)
            adjusted |= !Mathf.IsZeroApprox(_equalizer.GetBandGainDb(band));
        bool enabled = _equalizerToggleButton.ButtonPressed;
        AudioServer.SetBusEffectEnabled(_masterBusIndex, AudioUtils.AmplifyAudioEffectIndex,
            enabled && !Mathf.IsZeroApprox(_preamp.VolumeDb));
        AudioServer.SetBusEffectEnabled(_masterBusIndex, AudioUtils.Eq10AudioEffectIndex, enabled && adjusted);
        _graph.QueueRedraw();
    }

    /// <summary>Applies the on button's latched state to the equalizer effects.</summary>
    private void OnEqualizerToggleButtonPressed()
    {
        RefreshAudioEffects();
    }

    /// <summary>Applies the preamp gain to the master bus.</summary>
    /// <param name="value">Gain in decibels.</param>
    private void OnPreampSliderValueChanged(float value)
    {
        _preamp.VolumeDb = value;
        RefreshAudioEffects();
    }

    /// <summary>Applies a slider value to the band bound by its scene connection.</summary>
    /// <param name="value">Band gain in decibels.</param>
    /// <param name="bandIndex">Zero-based index in the ten-band equalizer.</param>
    private void OnBandValueChanged(float value, int bandIndex)
    {
        _equalizer.SetBandGainDb(bandIndex, value);
        RefreshAudioEffects();
    }
}
