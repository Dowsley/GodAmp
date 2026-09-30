using GodAmp.Audio.Processing;
using GodAmp.Presentation.Components;
using GodAmp.Skins;
using Godot;

namespace GodAmp.Presentation.Equalizer;

/// <summary>Displays shared EQ state and emits changes from expanded and compact controls.</summary>
public partial class Equalizer : WindowPanelContainer
{
    [Signal] public delegate void VolumeRequestedEventHandler(float value);
    [Signal] public delegate void BalanceRequestedEventHandler(float value);
    [Signal] public delegate void PreampRequestedEventHandler(float value);
    [Signal] public delegate void BandGainRequestedEventHandler(float value, int band);
    [Signal] public delegate void EnabledRequestedEventHandler(bool enabled);

    public override bool SupportsWindowshade => base.SupportsWindowshade && SkinLoader.Instance.EqualizerWindowshadeAvailable;
    [ExportGroup("References")]
    [Export] private AudioController _audio = null!;
    [Export] private TextureButton _equalizerToggleButton = null!;
    [Export] private EqualizerGraph _graph = null!;
    [Export] private SkinSlider _preampSlider = null!;
    [Export] private SkinSlider _volumeSlider = null!;
    [Export] private SkinSlider _balanceSlider = null!;
    [Export] private Godot.Collections.Array<SkinSlider> _bandSliders = [];

    public override void _Ready()
    {
        base._Ready();
        RefreshAudioState();
    }

    /// <summary>Synchronizes every EQ presentation without feeding reflected values back to the owner.</summary>
    public void RefreshAudioState()
    {
        _equalizerToggleButton.SetPressedNoSignal(_audio.EqualizerEnabled);
        _preampSlider.SetValueNoSignal(_audio.PreampDb);
        _volumeSlider.SetValueNoSignal(_audio.Volume);
        _balanceSlider.SetValueNoSignal(_audio.Balance);
        for (int band = 0; band < _bandSliders.Count; band++)
            _bandSliders[band].SetValueNoSignal(_audio.BandGains[band]);
        _graph.Refresh();
    }

    private void OnEqualizerToggleButtonPressed() => EmitSignal(SignalName.EnabledRequested, _equalizerToggleButton.ButtonPressed);
    private void OnPreampSliderValueChanged(float value) => EmitSignal(SignalName.PreampRequested, value);
    private void OnBandValueChanged(float value, int bandIndex) => EmitSignal(SignalName.BandGainRequested, value, bandIndex);
    private void OnVolumeChanged(float value) => EmitSignal(SignalName.VolumeRequested, value);
    private void OnBalanceChanged(float value) => EmitSignal(SignalName.BalanceRequested, value);
}
