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
    [Export] private HSlider _positionSeekerSlider = null!;
    [Export] private SkinnableHSlider _volumeSlider = null!;
    [Export] private SkinnableHSlider _pannerAudioSlider = null!;
    [Export] private Label _bitrateLabel = null!;
    [Export] private Label _sampleRateLabel = null!;
    [ExportSubgroup("Time display")]
    [Export] private Label _timeMinutesTensLabel = null!;
    [Export] private Label _timeMinutesOnesLabel = null!;
    [Export] private Label _timeSecondsTensLabel = null!;
    [Export] private Label _timeSecondsOnesLabel = null!;

    private TrackPlayer _trackPlayerRef = null!;
    private ButtonGroup _buttonGroup = null!;

    private bool _dragging = false;
    private bool _hasStarted = false;
    private float _resumeTrackAtPosition;
    private double _clockBlinkTimer = 1.0f;
    private bool _clockBlinking = false;

    public override void _Ready()
    {
        base._Ready();
        UIUtils.SetSliderColor(
            _pannerAudioSlider, (float)_pannerAudioSlider.Value, -1.0f, 1.0f);

        _buttonGroup = new ButtonGroup();
        _positionSeekerSlider.Value = 0.0f;

        UIUtils.SetSliderColor(
            _volumeSlider, (float)_volumeSlider.Value, 0.0f, 1.0f);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        var track = _trackPlayerRef.CurrentTrack;
        var hasTrack = track != null && _trackPlayerRef.Stream != null;

        _positionSeekerSlider.Editable = _hasStarted && hasTrack;
        _positionSeekerSlider.MinValue = 0.0f;
        _positionSeekerSlider.MaxValue = hasTrack ? _trackPlayerRef.Stream.GetLength() : 1.0;

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
        _clockBlinkTimer += delta;
    }

    private void UpdateTimeDisplay()
    {
        var playbackPosition = _trackPlayerRef.GetPlaybackPosition();
        var time = System.TimeSpan.FromSeconds(playbackPosition);

        int totalMinutes = (int)time.TotalMinutes;
        int seconds = time.Seconds;

        // Extract individual digits
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

    public void Setup(TrackPlayer trackPlayer)
    {
        _trackPlayerRef = trackPlayer;
    }

    public void Refresh()
    {
        _hasStarted = _trackPlayerRef.IsPlaying();
    }

    public void SetMasterLabelText(string text)
    {
        _masterLabel.SetValue(text);
    }

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

    private void OnStopTrackButtonPressed()
    {
        _trackPlayerRef.Stop();
        _hasStarted = false;
    }

    private static void OnPositionSeekerValueChanged(float value)
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.PositionSeekerChanged, value);
    }

    private void OnPositionSeekerDragStarted()
    {
        _dragging = true;
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LockMasterLabel, true);
    }

    private void OnPositionSeekerDragEnded(bool valueChanged)
    {
        _dragging = false;
        _resumeTrackAtPosition = (float)_positionSeekerSlider.Value;
        if (!_trackPlayerRef.StreamPaused)
        {
            _trackPlayerRef.Seek(_resumeTrackAtPosition);
        }
        OnSliderDragEnded();
    }

    private void OnVolumeSliderValueChanged(float value)
    {
        _trackPlayerRef.VolumeLinear = value;
        UIUtils.SetSliderColor(_volumeSlider, value, 0.0f, 1.0f);
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.VolumeChanged, value);
        SettingsManager.Instance.SetVolume(value);
    }

    private static void OnSliderDragStarted()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LockMasterLabel, false);
    }

    private static void OnSliderDragEnded(bool valueChanged = false)
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.UnlockMasterLabel);
    }

    private void OnPannerAudioSliderValueChanged(float value)
    {
        var busIndex = AudioServer.GetBusIndex("Master");
        if (AudioServer.GetBusEffect(busIndex, AudioUtils.PannerAudioEffectIndex) is AudioEffectPanner effect)
        {
            effect.Pan = value;
        }
        UIUtils.SetSliderColor(_pannerAudioSlider, value, -1.0f, 1.0f);
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

    private static void OnLoadTracksButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LoadTracksRequested, true);
    }
}