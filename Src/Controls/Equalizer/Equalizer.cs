using GodAmp.Components;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.Equalizer;

public partial class Equalizer : WindowPanelContainer
{
    [ExportGroup("References")]
    [Export] private VSlider _preampSlider = null!;
    [Export] private TextureButton _equalizerToggleButton = null!;

    public override void _Ready()
    {
        base._Ready();
        SetEqualizerEnabled(false);
        UIUtils.SetSliderColor(
            _preampSlider, (float)_preampSlider.Value, -12.0f, 12.0f);
    }

    private static void SetEqualizerEnabled(bool enabled)
    {
        var busIndex = AudioServer.GetBusIndex("Master");
        AudioServer.SetBusEffectEnabled(busIndex, 1, enabled);
        AudioServer.SetBusEffectEnabled(busIndex, 2, enabled);
    }

    private void OnEqualizerToggleButtonPressed()
    {
        var val = _equalizerToggleButton.ButtonPressed;
        SetEqualizerEnabled(val);
    }

    private void OnPreampSliderValueChanged(float value)
    {
        var busIndex = AudioServer.GetBusIndex("Master");
        if (AudioServer.GetBusEffect(busIndex, AudioUtils.AmplifyAudioEffectIndex) is AudioEffectAmplify effect)
        {
            effect.VolumeDb = value;
        }
        UIUtils.SetSliderColor(_preampSlider, value, -12.0f, 12.0f);
    }

    private static void On60SliderValueChanged(float value)
    {
        SetEq10Index(0, value);
    }

    private static void On170SliderValueChanged(float value)
    {
        SetEq10Index(1, value);
    }

    private static void On310SliderValueChanged(float value)
    {
        SetEq10Index(2, value);
    }

    private static void On600SliderValueChanged(float value)
    {
        SetEq10Index(3, value);
    }

    private static void On1KSliderValueChanged(float value)
    {
        SetEq10Index(4, value);
    }

    private static void On3KSliderValueChanged(float value)
    {
        SetEq10Index(5, value);
    }

    private static void On6KSliderValueChanged(float value)
    {
        SetEq10Index(6, value);
    }

    private static void On12KSliderValueChanged(float value)
    {
        SetEq10Index(7, value);
    }

    private static void On14SliderValueChanged(float value)
    {
        SetEq10Index(8, value);
    }

    private static void On16SliderValueChanged(float value)
    {
        SetEq10Index(9, value);
    }

    private static void SetEq10Index(int index, float value)
    {
        var busIndex = AudioServer.GetBusIndex("Master");
        if (AudioServer.GetBusEffect(busIndex, AudioUtils.Eq10AudioEffectIndex) is AudioEffectEQ10 effect)
        {
            effect.SetBandGainDb(index, value);
        }
    }
}