using GodAmp.Autoload;
using GodAmp.Components;
using GodAmp.Core;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.MasterPanel;

public partial class MasterPanel : WindowPanelContainer
{
    [Signal] public delegate void ToggleEqualizerRequestedEventHandler();
    [Signal] public delegate void TogglePlaylistRequestedEventHandler();
    [Signal] public delegate void PlayRequestedEventHandler();
    [Signal] public delegate void PauseRequestedEventHandler();
    [Signal] public delegate void StopRequestedEventHandler();
    [Signal] public delegate void NextRequestedEventHandler();
    [Signal] public delegate void PreviousRequestedEventHandler();
    [Signal] public delegate void SeekRequestedEventHandler(float position);
    [Signal] public delegate void ShuffleRequestedEventHandler(bool enabled);
    [Signal] public delegate void RepeatRequestedEventHandler(bool enabled);

    [ExportGroup("Config")]
    [Export] public double ClockBlinkEverySeconds = 1.0f;

    [ExportGroup("References")]
    [Export] private PlaybackController _playbackController = null!;
    [Export] private TrackPlayer _trackPlayerRef = null!;
    [Export] private TextureButton _shuffleButton = null!;
    [Export] private TextureButton _repeatButton = null!;
    [Export] public WinampMenuButton WinampMenuButton = null!;
    [Export] public TextureButton ToggleEqualizerButton = null!;
    [Export] public TextureButton TogglePlaylistButton = null!;
    [Export] private MarqueeLabel _masterLabel = null!;
    [Export] private SkinSlider _positionSeekerSlider = null!;
    [Export] private SkinSlider _volumeSlider = null!;
    [Export] private SkinSlider _pannerAudioSlider = null!;
    [Export] private Label _bitrateLabel = null!;
    [Export] private Label _sampleRateLabel = null!;
    [Export] private SkinSlider _windowshadeSeek = null!;
    [Export] private Label _windowshadeTime = null!;
    [ExportSubgroup("Time display")]
    [Export] private Label _timeMinutesTensLabel = null!;
    [Export] private Label _timeMinutesOnesLabel = null!;
    [Export] private Label _timeSecondsTensLabel = null!;
    [Export] private Label _timeSecondsOnesLabel = null!;

    private bool _dragging = false;
    private double _clockBlinkTimer = 1.0f;
    private bool _clockBlinking = false;

    /// <inheritdoc />
    public override void _Ready()
    {
        base._Ready();
        _positionSeekerSlider.Value = 0.0f;
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        base._Process(delta);

        var track = _trackPlayerRef.CurrentTrack;
        var stream = _trackPlayerRef.Stream;
        var hasTrack = track != null && stream != null;

        bool hasStarted = _playbackController.State != PlaybackState.Stopped;
        _positionSeekerSlider.Editable = hasStarted && hasTrack;
        _positionSeekerSlider.MinValue = 0.0f;
        _positionSeekerSlider.MaxValue = hasTrack ? stream!.GetLength() : 1.0;
        _windowshadeSeek.Editable = _positionSeekerSlider.Editable;
        _windowshadeSeek.MaxValue = _positionSeekerSlider.MaxValue;

        _bitrateLabel.Text = hasTrack ? $"{track!.BitrateKbps}" : "0";
        _sampleRateLabel.Text = hasTrack ? $"{track!.SampleRateHz / 1000}" : "0";
        if (hasStarted && hasTrack)
        {
            if (!_dragging)
            {
                _positionSeekerSlider.Value = _playbackController.Position;
            }
        }
        else
        {
            _positionSeekerSlider.Value = 0.0f;
        }

        UpdateTimeDisplay();
        _windowshadeSeek.SetValueNoSignal(_positionSeekerSlider.Value);
        _clockBlinkTimer += delta;
    }

    /// <summary>Updates expanded digits and compact bitmap time from the shared playback position.</summary>
    private void UpdateTimeDisplay()
    {
        var playbackPosition = _playbackController.Position;
        var time = System.TimeSpan.FromSeconds(playbackPosition);

        int totalMinutes = (int)time.TotalMinutes;
        int seconds = time.Seconds;

        _windowshadeTime.Text = $"{totalMinutes,3}:{seconds:00}";
        int minutesTens = totalMinutes / 10;
        int minutesOnes = totalMinutes % 10;
        int secondsTens = seconds / 10;
        int secondsOnes = seconds % 10;

        _timeMinutesTensLabel.Text = minutesTens.ToString();
        _timeMinutesOnesLabel.Text = minutesOnes.ToString();
        _timeSecondsTensLabel.Text = secondsTens.ToString();
        _timeSecondsOnesLabel.Text = secondsOnes.ToString();

        if (_playbackController.State == PlaybackState.Playing)
        {
            _timeMinutesTensLabel.Modulate = new Color(_timeMinutesTensLabel.Modulate, 1.0f);
            _timeMinutesOnesLabel.Modulate = new Color(_timeMinutesOnesLabel.Modulate, 1.0f);
            _timeSecondsTensLabel.Modulate = new Color(_timeSecondsTensLabel.Modulate, 1.0f);
            _timeSecondsOnesLabel.Modulate = new Color(_timeSecondsOnesLabel.Modulate, 1.0f);
        }
        else
        {
            if (!(_clockBlinkTimer > ClockBlinkEverySeconds))
                return;

            float alpha = _clockBlinking ? 0.5f : 1.0f;
            _timeMinutesTensLabel.Modulate = new Color(_timeMinutesTensLabel.Modulate, alpha);
            _timeMinutesOnesLabel.Modulate = new Color(_timeMinutesOnesLabel.Modulate, alpha);
            _timeSecondsTensLabel.Modulate = new Color(_timeSecondsTensLabel.Modulate, alpha);
            _timeSecondsOnesLabel.Modulate = new Color(_timeSecondsOnesLabel.Modulate, alpha);

            _clockBlinking = !_clockBlinking;
            _clockBlinkTimer = 0.0;
        }
    }

    /// <summary>Reflects navigation policy without emitting another toggle request.</summary>
    public void RefreshModes()
    {
        _shuffleButton.SetPressedNoSignal(_playbackController.ShuffleEnabled);
        _repeatButton.SetPressedNoSignal(_playbackController.RepeatEnabled);
    }

    public void SetMasterLabelText(string text)
    {
        _masterLabel.SetValue(text);
    }

    private void OnPlayTrackButtonPressed() => EmitSignal(SignalName.PlayRequested);
    private void OnPauseTrackButtonPressed() => EmitSignal(SignalName.PauseRequested);
    private void OnStopTrackButtonPressed() => EmitSignal(SignalName.StopRequested);

    private static void OnPositionSeekerValueChanged(float value)
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.PositionSeekerChanged, value);
    }

    /// <summary>Routes compact seeking through the expanded slider's existing playback handlers.</summary>
    /// <param name="value">Requested playback position in seconds.</param>
    private void OnWindowshadeSeekValueChanged(float value) => _positionSeekerSlider.Value = value;

    /// <summary>Updates both presentations from a scene-connected compact volume control.</summary>
    /// <param name="value">Requested linear volume between zero and one.</param>
    private void OnWindowshadeVolumeChanged(float value) => _volumeSlider.Value = value;

    /// <summary>Updates the existing balance control from the compact EQ presentation.</summary>
    /// <param name="value">Requested stereo balance from minus one to one.</param>
    private void OnWindowshadeBalanceChanged(float value) => _pannerAudioSlider.Value = value;

    private void OnPositionSeekerDragStarted()
    {
        _dragging = true;
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LockMasterLabel, true);
    }

    /// <summary>Seeks after the pointer or keyboard interaction and unlocks the marquee.</summary>
    /// <param name="_">Signal payload indicating whether the slider value changed.</param>
    private void OnPositionSeekerDragEnded(bool _)
    {
        _dragging = false;
        EmitSignal(SignalName.SeekRequested, (float)_positionSeekerSlider.Value);
        OnSliderDragEnded();
    }

    /// <summary>Applies the volume and persists the setting.</summary>
    /// <param name="value">Linear volume from zero to one.</param>
    private void OnVolumeSliderValueChanged(float value)
    {
        _trackPlayerRef.VolumeLinear = value;
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.VolumeChanged, value);
        SettingsManager.Instance.SetVolume(value);
    }

    private static void OnSliderDragStarted()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LockMasterLabel, false);
    }

    /// <summary>Restores the track title after a slider interaction.</summary>
    /// <param name="_">Optional change flag supplied by slider signals.</param>
    private static void OnSliderDragEnded(bool _ = false)
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.UnlockMasterLabel);
    }

    /// <summary>Updates the master bus balance and its display notification.</summary>
    /// <param name="value">Balance from minus one (left) to one (right).</param>
    private static void OnPannerAudioSliderValueChanged(float value)
    {
        var busIndex = AudioServer.GetBusIndex("Master");
        if (AudioServer.GetBusEffect(busIndex, AudioUtils.PannerAudioEffectIndex) is AudioEffectPanner effect)
        {
            effect.Pan = value;
        }
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.PannerBalanceChanged, value);
    }

    public void SetVolumeValue(float volume)
    {
        _volumeSlider.Value = volume;
    }

    private void OnNextTrackButtonPressed() => EmitSignal(SignalName.NextRequested);
    private void OnPreviousTrackButtonPressed() => EmitSignal(SignalName.PreviousRequested);
    private void OnShuffleModeToggled(bool enabled) => EmitSignal(SignalName.ShuffleRequested, enabled);
    private void OnRepeatModeToggled(bool enabled) => EmitSignal(SignalName.RepeatRequested, enabled);

    private void OnToggleEqualizerButton()
    {
        EmitSignal(SignalName.ToggleEqualizerRequested);
    }

    private void OnTogglePlaylistButton()
    {
        EmitSignal(SignalName.TogglePlaylistRequested);
    }

    public override void OnCloseButtonPressed()
    {
        GetTree().Quit();
    }

    /// <summary>Minimizes the main native window using Godot's desktop window state.</summary>
    private void OnIconifyButtonPressed() => WindowRef.Mode = Window.ModeEnum.Minimized;

    private static void OnLoadTracksButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LoadTracksRequested, true);
    }
}
