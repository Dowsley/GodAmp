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

    [ExportGroup("Config")]
    [Export] public double ClockBlinkEverySeconds = 1.0f;

    [ExportGroup("References")]
    [Export] public WinampMenuButton WinampMenuButton = null!;
    [Export] public TextureButton ToggleEqualizerButton = null!;
    [Export] public TextureButton TogglePlaylistButton = null!;
    [Export] private MarqueeLabel _masterLabel = null!;
    [Export] private SkinSlider _positionSeekerSlider = null!;
    [Export] private SkinSlider _volumeSlider = null!;
    [Export] private SkinSlider _pannerAudioSlider = null!;
    [Export] private Label _bitrateLabel = null!;
    [Export] private Label _sampleRateLabel = null!;
    [Export] private PlaybackIndicators _playbackIndicators = null!;
    [Export] private ClassicVisualization _visualization = null!;
    [Export] private SkinSlider _windowshadeSeek = null!;
    [Export] private Label _windowshadeTime = null!;
    [ExportSubgroup("Time display")]
    [Export] private Label _timeMinutesTensLabel = null!;
    [Export] private Label _timeMinutesOnesLabel = null!;
    [Export] private Label _timeSecondsTensLabel = null!;
    [Export] private Label _timeSecondsOnesLabel = null!;

    private TrackPlayer _trackPlayerRef = null!;

    private bool _dragging = false;
    private bool _hasStarted = false;
    private float _resumeTrackAtPosition;
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

        _positionSeekerSlider.Editable = _hasStarted && hasTrack;
        _positionSeekerSlider.MinValue = 0.0f;
        _positionSeekerSlider.MaxValue = hasTrack ? stream!.GetLength() : 1.0;
        _windowshadeSeek.Editable = _positionSeekerSlider.Editable;
        _windowshadeSeek.MaxValue = _positionSeekerSlider.MaxValue;

        _bitrateLabel.Text = hasTrack ? $"{track!.BitrateKbps}" : "0";
        _sampleRateLabel.Text = hasTrack ? $"{track!.SampleRateHz / 1000}" : "0";
        if (_hasStarted && hasTrack)
        {
            if (!_dragging && !_trackPlayerRef.StreamPaused)
            {
                _positionSeekerSlider.Value = _trackPlayerRef.GetPlaybackPosition();
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
        var playbackPosition = _trackPlayerRef.GetPlaybackPosition();
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

        if (_trackPlayerRef.IsPlaying() && !_trackPlayerRef.StreamPaused)
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

    /// <summary>Connects the panel's runtime playback source to its controls and displays.</summary>
    /// <param name="trackPlayer">Application-owned audio player.</param>
    public void Setup(TrackPlayer trackPlayer)
    {
        _trackPlayerRef = trackPlayer;
        _playbackIndicators.Player = trackPlayer;
        _visualization.Player = trackPlayer;
    }

    public void Refresh()
    {
        _hasStarted = _trackPlayerRef.IsPlaying();
    }

    public void SetMasterLabelText(string text)
    {
        _masterLabel.SetValue(text);
    }

    /// <summary>Starts the current track or requests a track picker when the playlist has no current track.</summary>
    private void OnPlayTrackButtonPressed()
    {
        if (_trackPlayerRef.CurrentTrack == null)
        {
            SignalBus.Instance.EmitSignal(SignalBus.SignalName.LoadTracksRequested, true);
            return;
        }
        _trackPlayerRef.Play(0.0f);
        _hasStarted = true;
    }

    /// <summary>Toggles pause while retaining the seek position for either presentation.</summary>
    private void OnPauseTrackButtonPressed()
    {
        _trackPlayerRef.StreamPaused = !_trackPlayerRef.StreamPaused;
        if (_trackPlayerRef.StreamPaused)
        {
            _resumeTrackAtPosition = (float)_positionSeekerSlider.Value;
        }
        else
        {
            _trackPlayerRef.Seek(_resumeTrackAtPosition);
        }
    }

    /// <summary>Stops the shared player and resets seek availability.</summary>
    private void OnStopTrackButtonPressed()
    {
        _trackPlayerRef.Stop();
        _hasStarted = false;
    }

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
        _resumeTrackAtPosition = (float)_positionSeekerSlider.Value;
        if (!_trackPlayerRef.StreamPaused)
        {
            _trackPlayerRef.Seek(_resumeTrackAtPosition);
        }
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

    private static void OnNextTrackButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.NextTrackRequested);
    }

    private static void OnPreviousTrackButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.PreviousTrackRequested);
    }

    private static void OnShuffleModeButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.ShuffleModeRequested);
    }

    private static void OnRepeatModeButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.RepeatModeRequested);
    }

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
