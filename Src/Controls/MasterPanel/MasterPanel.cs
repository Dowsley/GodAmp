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
	
	[Export] public double ClockBlinkEverySeconds = 1.0f;

	public TextureButton ToggleEqualizerButton;
	public TextureButton TogglePlaylistButton;

	private MarqueeLabel _masterLabel;
	private Label _bitrateLabel;
	private Label _sampleRateLabel;
	private Label _clockLabel;
	private CheckButton _1XZoomButton;
	private CheckButton _2XZoomButton;
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

	private Vector2I _initialSize;
	
	public override void _Ready()
	{
		_positionSeekerSlider = GetNode<HSlider>("%PositionSeeker");
		_masterLabel = GetNode<MarqueeLabel>("%MasterLabel");
		_bitrateLabel = GetNode<Label>("%BitrateLabel");
		_sampleRateLabel = GetNode<Label>("%SampleRateLabel");
		_clockLabel = GetNode<Label>("%ClockLabel");
		_volumeSlider = GetNode<HSlider>("%VolumeSlider");
		_1XZoomButton = GetNode<CheckButton>("%1XZoomButton");
		_2XZoomButton = GetNode<CheckButton>("%2XZoomButton");
		_pannerAudioSlider = GetNode<HSlider>("%PannerAudioSlider");
		ToggleEqualizerButton = GetNode<TextureButton>("%ToggleEqualizerButton");
		TogglePlaylistButton = GetNode<TextureButton>("%TogglePlaylistButton");
		UIUtils.SetSliderColor(
			_pannerAudioSlider, (float)_pannerAudioSlider.Value, -1.0f, 1.0f);

		_buttonGroup = new ButtonGroup();

		_1XZoomButton.ButtonGroup = _buttonGroup;
		_2XZoomButton.ButtonGroup = _buttonGroup;

		_1XZoomButton.ToggleMode = true;
		_2XZoomButton.ToggleMode = true;

		_initialSize = GetWindow().Size;
		_1XZoomButton.ButtonPressed = true;
		Set1XZoomMode();
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
		_bitrateLabel.Text = $"{track.BitrateKbps}";
		_sampleRateLabel.Text = $"{track.SampleRateHz}";
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
		_clockLabel.Text = TimeUtils.FormatAsTrackTime(_trackPlayerRef.GetPlaybackPosition(), 2);
		if (_trackPlayerRef.IsPlaying() && !_trackPlayerRef.StreamPaused)
		{
			_clockLabel.Modulate = new Color(_clockLabel.Modulate, 1.0f);
		}
		else
		{
			if (!(_clockBlinkTimer > ClockBlinkEverySeconds))
				return;
			_clockLabel.Modulate = new Color(_clockLabel.Modulate, _clockBlinking ? 0.5f : 1.0f);
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

	public void OnPlayTrackButtonPressed()
	{
		_trackPlayerRef.Play(0.0f);
		_hasStarted = true;
	}
	
	public void OnPauseTrackButtonPressed()
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

	public void OnStopTrackButtonPressed()
	{
		_trackPlayerRef.Stop();
		_hasStarted = false;
	}

	public void OnPositionSeekerValueChanged(float value)
	{
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.PositionSeekerChanged, value);
	}

	public void OnPositionSeekerDragStarted()
	{
		_dragging = true;
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.LockMasterLabel, true);
	}
	
	public void OnPositionSeekerDragEnded(bool valueChanged)
	{
		_dragging = false;
		_resumeTrackAtPosition = (float)_positionSeekerSlider.Value;
		if (!_trackPlayerRef.StreamPaused)
		{
			_trackPlayerRef.Seek(_resumeTrackAtPosition);
		}
		OnSliderDragEnded();
	}
	
	public void OnVolumeSliderValueChanged(float value)
	{
		_trackPlayerRef.VolumeLinear = value;
		UIUtils.SetSliderColor(_volumeSlider, value, 0.0f, 1.0f);
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.VolumeChanged, value);
	}

	public void OnSliderDragStarted()
	{
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.LockMasterLabel, false);
	}
	
	public void OnSliderDragEnded(float value = 0.0f)
	{
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.UnlockMasterLabel);
	}

	public void OnPannerAudioSliderValueChanged(float value)
	{
		var busIndex = AudioServer.GetBusIndex("Master");
		if (AudioServer.GetBusEffect(busIndex, AudioUtils.PannerAudioEffectIndex) is AudioEffectPanner effect)
		{
			effect.Pan = value;
		}
		UIUtils.SetSliderColor(_pannerAudioSlider, value, -1.0f, 1.0f);
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.PannerBalanceChanged, value);
	}

	public void Set1XZoomMode()
	{
		GetWindow().Size = _initialSize;
	}

	public void Set2XZoomMode()
	{
		GetWindow().Size = new Vector2I(_initialSize.X*2, _initialSize.Y*2);
	}

	public void OnNextTrackButtonPressed()
	{
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.NextTrackRequested);
	}
	
	public void OnPreviousTrackButtonPressed()
	{
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.PreviousTrackRequested);
	}
	
	public void OnShuffleModeButtonPressed()
	{
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.ShuffleModeRequested);
	}
	
	public void OnRepeatModeButtonPressed()
	{
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.RepeatModeRequested);
	}

	public void OnToggleEqualizerButton()
	{
		EmitSignal(SignalName.ToggleEqualizerRequested);
	}
	
	public void OnTogglePlaylistButton()
	{
		EmitSignal(SignalName.TogglePlaylistRequested);
	}

	public override void OnCloseButtonPressed()
	{
		GetTree().Quit();
	}

	public void OnLoadTracksButtonPressed()
	{
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.LoadTracksRequested);
	}
}