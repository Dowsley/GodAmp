using System;
using GodAmp.Autoload;
using GodAmp.Components;
using GodAmp.Player;
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
	[Export] public MenuButton WinampMenuButton;

	public TextureButton ToggleEqualizerButton;
	public TextureButton TogglePlaylistButton;

	// TODO: Reactivate those commented out fields.
	private MarqueeLabel _masterLabel;
	// private Label _bitrateLabel;
	// private Label _sampleRateLabel;
	// private Label _clockLabel;
	private ButtonGroup _buttonGroup;
	private HSlider _positionSeekerSlider;
	private HSlider _volumeSlider;
	private HSlider _pannerAudioSlider;
	private TrackPlayer _trackPlayerRef;
	
	private bool _dragging = false;
	private bool _hasStarted = false;
	private float _resumeTrackAtPosition;
	private double _clockBlinkTimer = 1.0f;
	private bool _clockBlinking = false;
	
	public override void _Ready()
	{
		_positionSeekerSlider = GetNode<HSlider>("%PositionSeeker");
		_masterLabel = GetNode<MarqueeLabel>("%MasterLabel");
		// _bitrateLabel = GetNode<Label>("%BitrateLabel");
		// _sampleRateLabel = GetNode<Label>("%SampleRateLabel");
		// _clockLabel = GetNode<Label>("%ClockLabel");
		_volumeSlider = GetNode<HSlider>("%VolumeSlider");
		_pannerAudioSlider = GetNode<HSlider>("%PannerAudioSlider");
		ToggleEqualizerButton = GetNode<TextureButton>("%ToggleEqualizerButton");
		TogglePlaylistButton = GetNode<TextureButton>("%TogglePlaylistButton");
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
		
		_positionSeekerSlider.Editable = _hasStarted;
		_positionSeekerSlider.MinValue = 0.0f;
		_positionSeekerSlider.MaxValue = _trackPlayerRef.Stream.GetLength();

		var track = _trackPlayerRef.CurrentTrack;
		// _bitrateLabel.Text = $"{track.BitrateKbps}";
		// _sampleRateLabel.Text = $"{track.SampleRateHz}";
		if (_hasStarted)
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

		_clockBlinkTimer += delta;
		// _clockLabel.Text = TimeUtils.FormatAsTrackTime(_trackPlayerRef.GetPlaybackPosition(), 2);
		if (_trackPlayerRef.IsPlaying() && !_trackPlayerRef.StreamPaused)
		{
			// _clockLabel.Modulate = new Color(_clockLabel.Modulate, 1.0f);
		}
		else
		{
			if (!(_clockBlinkTimer > ClockBlinkEverySeconds))
				return;
			// _clockLabel.Modulate = new Color(_clockLabel.Modulate, _clockBlinking ? 0.5f : 1.0f);
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
	
	private static void OnSliderDragEnded(float value = 0.0f)
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