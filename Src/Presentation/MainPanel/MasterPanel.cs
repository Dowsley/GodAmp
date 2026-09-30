using System;
using System.Linq;
using GodAmp.Presentation.Components;
using GodAmp.Desktop;
using GodAmp.Media;
using GodAmp.Playback;
using GodAmp.Audio.Processing;
using GodAmp.Presentation.Formatting;
using Godot;

namespace GodAmp.Presentation.MainPanel;

/// <summary>Displays transport and audio state and routes player input through scene-authored signals.</summary>
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
    [Signal] public delegate void FilesRequestedEventHandler(bool replace);
    [Signal] public delegate void VolumeRequestedEventHandler(float value);
    [Signal] public delegate void BalanceRequestedEventHandler(float value);

    [ExportGroup("References")]
    [Export] private PlaybackController _playbackController = null!;
    [Export] private AudioController _audio = null!;
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
    [ExportSubgroup("Time display")]
    [Export] private PlaybackClock _playbackClock = null!;
    [Export] private CompactPlaybackClock _compactClock = null!;

    private bool _dragging = false;
    private enum LabelDisplay { Track, Slider, Seek }
    private LabelDisplay _labelDisplay;

    /// <summary>Reflects the window owner's visibility state in the panel's controls.</summary>
    /// <param name="window">Window whose visibility was published.</param>
    /// <param name="visible">Actual native visibility.</param>
    public void RefreshWindowVisibility(PlayerWindow window, bool visible)
    {
        switch (window)
        {
            case PlayerWindow.Equalizer:
                ToggleEqualizerButton.SetPressedNoSignal(visible);
                WinampMenuButton.SetEqualizerChecked(visible);
                break;
            case PlayerWindow.Playlist:
                TogglePlaylistButton.SetPressedNoSignal(visible);
                WinampMenuButton.SetPlaylistChecked(visible);
                break;
            case PlayerWindow.Visualizer:
                WinampMenuButton.SetVisualizerChecked(visible);
                break;
        }
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        base._Ready();
        _positionSeekerSlider.Value = 0.0f;
        RefreshTrackTitle();
        RefreshTrackPresentation();
        RefreshAudioState();
        RefreshModes();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        base._Process(delta);

        if (_playbackController.CanSeek)
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

        double position = _playbackController.Position;
        _playbackClock.UpdateDisplay(position, _playbackController.State == PlaybackState.Playing, delta);
        _compactClock.UpdateDisplay(position);
        _windowshadeSeek.SetValueNoSignal(_positionSeekerSlider.Value);
    }

    /// <summary>Reflects source metadata and seek availability when playback inputs change.</summary>
    public void RefreshTrackPresentation()
    {
        var track = _playbackController.CurrentEntry?.Track;
        _bitrateLabel.Text = track?.BitrateKbps.ToString() ?? "0";
        _sampleRateLabel.Text = track == null ? "0" : (track.SampleRateHz / 1000).ToString();
        _positionSeekerSlider.Editable = _playbackController.CanSeek;
        float duration = _playbackController.CurrentDuration;
        _positionSeekerSlider.MaxValue = duration > 0 ? duration : 1.0;
        _windowshadeSeek.Editable = _positionSeekerSlider.Editable;
        _windowshadeSeek.MaxValue = _positionSeekerSlider.MaxValue;
    }

    /// <summary>Updates the main display only when metadata affects the current occurrence.</summary>
    /// <param name="entryIds">Queue occurrences whose metadata changed.</param>
    public void RefreshMetadata(long[] entryIds)
    {
        if (_playbackController.CurrentEntry is not { } current || !entryIds.Contains(current.Id))
            return;
        RefreshTrackTitle();
        RefreshTrackPresentation();
    }

    /// <summary>Reflects navigation policy without emitting another toggle request.</summary>
    public void RefreshModes()
    {
        _shuffleButton.SetPressedNoSignal(_playbackController.ShuffleEnabled);
        _repeatButton.SetPressedNoSignal(_playbackController.RepeatEnabled);
    }

    /// <summary>Displays track or interaction feedback in the scrolling title.</summary>
    /// <param name="text">Unformatted display text.</param>
    public void SetMasterLabelText(string text)
    {
        _masterLabel.SetValue(text);
    }

    private void OnPlayTrackButtonPressed() => EmitSignal(SignalName.PlayRequested);

    /// <summary>Reflects the selected entry unless a slider temporarily owns the marquee.</summary>
    public void RefreshTrackTitle()
    {
        if (_labelDisplay != LabelDisplay.Track)
            return;
        var entry = _playbackController.CurrentEntry;
        SetMasterLabelText(entry == null ? "" : TrackTitle.FormatNumbered(entry.Track, _playbackController.CurrentIndex + 1));
    }
    private void OnPauseTrackButtonPressed() => EmitSignal(SignalName.PauseRequested);
    private void OnStopTrackButtonPressed() => EmitSignal(SignalName.StopRequested);

    private void OnPositionSeekerValueChanged(float value)
    {
        float duration = _playbackController.CurrentDuration;
        if (_labelDisplay == LabelDisplay.Seek && _playbackController.CanSeek && duration > 0)
            SetMasterLabelText($"SEEK TO: {TrackTime.Format(value)}/{TrackTime.Format(duration)} ({value / duration * 100:F0}%)");
    }

    /// <summary>Routes compact seeking through the expanded slider's existing playback handlers.</summary>
    /// <param name="value">Requested playback position in seconds.</param>
    private void OnWindowshadeSeekValueChanged(float value) => _positionSeekerSlider.Value = value;

    private void OnPositionSeekerDragStarted()
    {
        _dragging = true;
        _labelDisplay = LabelDisplay.Seek;
    }

    /// <summary>Seeks after the pointer or keyboard interaction and unlocks the marquee.</summary>
    /// <param name="_">Signal payload indicating whether the slider value changed.</param>
    private void OnPositionSeekerDragEnded(bool _)
    {
        _dragging = false;
        EmitSignal(SignalName.SeekRequested, (float)_positionSeekerSlider.Value);
        OnSliderDragEnded();
    }

    /// <summary>Requests shared volume and displays feedback during an active slider interaction.</summary>
    /// <param name="value">Normalized volume slider position from zero to one.</param>
    private void OnVolumeSliderValueChanged(float value)
    {
        EmitSignal(SignalName.VolumeRequested, value);
        if (_labelDisplay == LabelDisplay.Slider)
            SetMasterLabelText($"VOLUME: {Convert.ToInt64(value * 100)}%");
    }

    private void OnSliderDragStarted()
    {
        _labelDisplay = LabelDisplay.Slider;
    }

    /// <summary>Restores the track title after a slider interaction.</summary>
    /// <param name="_">Optional change flag supplied by slider signals.</param>
    private void OnSliderDragEnded(bool _ = false)
    {
        _labelDisplay = LabelDisplay.Track;
        RefreshTrackTitle();
    }

    /// <summary>Requests shared balance and displays feedback during an active slider interaction.</summary>
    /// <param name="value">Balance from minus one (left) to one (right).</param>
    private void OnPannerAudioSliderValueChanged(float value)
    {
        EmitSignal(SignalName.BalanceRequested, value);
        if (_labelDisplay == LabelDisplay.Slider)
            SetMasterLabelText(Mathf.IsZeroApprox(value) ? "BALANCE: CENTER"
                : $"BALANCE: {Convert.ToInt64(float.Abs(value) * 100)}% " + (value < 0 ? "LEFT" : "RIGHT"));
    }

    /// <summary>Reflects shared audio state without producing another change request.</summary>
    public void RefreshAudioState()
    {
        _volumeSlider.SetValueNoSignal(_audio.Volume);
        _pannerAudioSlider.SetValueNoSignal(_audio.Balance);
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

    private void OnLoadTracksButtonPressed() => EmitSignal(SignalName.FilesRequested, true);
}
