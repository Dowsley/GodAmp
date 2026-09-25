using GodAmp.Components;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.Equalizer;

/// <summary>Applies the skinned equalizer controls to the master audio bus.</summary>
public partial class Equalizer : WindowPanelContainer
{
    [ExportGroup("References")]
    [Export] private TextureButton _equalizerToggleButton = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        base._Ready();
        SetEqualizerEnabled(false);
    }

    /// <summary>Enables or bypasses the preamp and equalizer together.</summary>
    /// <param name="enabled">Whether equalization should affect playback.</param>
    private static void SetEqualizerEnabled(bool enabled)
    {
        var busIndex = AudioServer.GetBusIndex("Master");
        AudioServer.SetBusEffectEnabled(busIndex, AudioUtils.AmplifyAudioEffectIndex, enabled);
        AudioServer.SetBusEffectEnabled(busIndex, AudioUtils.Eq10AudioEffectIndex, enabled);
    }

    /// <summary>Applies the on button's latched state to the equalizer effects.</summary>
    private void OnEqualizerToggleButtonPressed()
    {
        var val = _equalizerToggleButton.ButtonPressed;
        SetEqualizerEnabled(val);
    }

    /// <summary>Applies the preamp gain to the master bus.</summary>
    /// <param name="value">Gain in decibels.</param>
    private static void OnPreampSliderValueChanged(float value)
    {
        var busIndex = AudioServer.GetBusIndex("Master");
        if (AudioServer.GetBusEffect(busIndex, AudioUtils.AmplifyAudioEffectIndex) is AudioEffectAmplify effect)
        {
            effect.VolumeDb = value;
        }
    }

    /// <summary>Applies a slider value to the band bound by its scene connection.</summary>
    /// <param name="value">Band gain in decibels.</param>
    /// <param name="bandIndex">Zero-based index in the ten-band equalizer.</param>
    private static void OnBandValueChanged(float value, int bandIndex)
    {
        var busIndex = AudioServer.GetBusIndex("Master");
        if (AudioServer.GetBusEffect(busIndex, AudioUtils.Eq10AudioEffectIndex) is AudioEffectEQ10 effect)
        {
            effect.SetBandGainDb(bandIndex, value);
        }
    }
}
