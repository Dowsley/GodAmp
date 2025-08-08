using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using GodAmp.Autoload;
using GodAmp.Data;
using GodAmp.Player;
using GodAmp.Utils;
using Godot;

namespace GodAmp;

public partial class Main : CenterContainer
{
	[ExportGroup("Config")]
	[Export] public string DefaultSongsPath;
	
	private Controls.Controls _controls;
	private Visualizer.Visualizer _visualizer;
	private TrackPlayer _trackPlayer;

	private List<Track> _playlist;
	private int _currentTrackIndex = 0;

	private bool _repeatMode = false;
	private bool _shuffleMode = false;
	private int _randomizedTrackIndex = 0;
	private List<int> _randomizedTrackIndices = [];
	private bool _masterLabelLocked = false;
	private bool _masterLabelLockedByPositionSeeker = false;

	private FileDialog _lastUsedFileDialog;
	
	public override void _Ready()
	{
		_controls = GetNode<Controls.Controls>("%Controls");
		_visualizer = GetNode<Visualizer.Visualizer>("%Visualizer");
		_trackPlayer = GetNode<TrackPlayer>("%TrackPlayer");

		_playlist = AudioUtils.LoadAllTracksFromDir(DefaultSongsPath);
		if (_playlist.Count == 0)
		{
			GD.PrintErr("No tracks found in the default songs path.");
		}
		_playlist.Sort((a, b) => a.TrackNumber - b.TrackNumber);
		_trackPlayer.SetCurrentTrack(_playlist[_currentTrackIndex], false);
		_visualizer.Pause();
		
		_controls.Setup(_trackPlayer, _playlist);
		_controls.Refresh();
		
		SignalBus.Instance.NextTrackRequested += OnNextTrackRequested;
		SignalBus.Instance.PreviousTrackRequested += OnPreviousTrackRequested;
		SignalBus.Instance.ShuffleModeRequested += OnShuffleModeRequested;
		SignalBus.Instance.RepeatModeRequested += OnRepeatModeRequested;
		SignalBus.Instance.ChangeToTrackRequested += OnChangeToTrackRequested;
		SignalBus.Instance.LockMasterLabel += LockMasterLabel;
		SignalBus.Instance.UnlockMasterLabel += UnlockMasterLabel;
		SignalBus.Instance.VolumeChanged += OnVolumeChanged;
		SignalBus.Instance.PannerBalanceChanged += OnPannerBalanceChanged;
		SignalBus.Instance.PositionSeekerChanged += OnPositionSeekerChanged;
		SignalBus.Instance.LoadTracksRequested += OnLoadTracksRequested;
	}

	public override void _Process(double delta)
	{
		if (_trackPlayer.IsPlaying())
			_visualizer.Unpause();
		else
			_visualizer.Pause();
		
		if (!_masterLabelLocked)
		{
			_controls.SetMasterLabelText(AudioUtils.GetFullTrackTitle(_trackPlayer.CurrentTrack));
		}
	}

	public void OnNextTrackRequested()
	{
		NextTrack();
	}

	public void OnPreviousTrackRequested()
	{
		PreviousTrack();
	}
	
	public void OnTrackPlayerFinished()
	{
		NextTrack(true);
	}

	public void OnShuffleModeRequested()
	{
		_shuffleMode = !_shuffleMode;
		if (_shuffleMode)
		{
			_randomizedTrackIndex = 0;
			var random = new Random();
			_randomizedTrackIndices = Enumerable.Range(0, _playlist.Count).OrderBy(_ => random.Next()).ToList();
		}
		else
		{
			var realIndexToResumeOn = _randomizedTrackIndices[_randomizedTrackIndex];
			_currentTrackIndex = realIndexToResumeOn;
		}
	}

	public void OnRepeatModeRequested()
	{
		_repeatMode = !_repeatMode;
	}

	public void OnChangeToTrackRequested(int index)
	{
		ChangeToTrack(index, true);
	}

	public void OnVolumeChanged(float volume)
	{
		_controls.SetMasterLabelText($"VOLUME: {Convert.ToInt64(volume * 100)}%");
	}
	
	public void OnPannerBalanceChanged(float value)
	{
		var text = "";
		if (Mathf.IsZeroApprox(value))
		{
			text = "BALANCE: CENTER";
		}
		else
		{
			text = $"BALANCE: {Convert.ToInt64(float.Abs(value) * 100)}% " + (value < 0.0f ? "LEFT" : "RIGHT");
		}
		_controls.SetMasterLabelText(text);
	}
	
	public void OnPositionSeekerChanged(float value)
	{
		var totalTimeSecs = _trackPlayer.CurrentTrack.Duration;
		if (_masterLabelLocked && _masterLabelLockedByPositionSeeker)
			_controls.SetMasterLabelText(
				$"SEEK TO: {TimeUtils.FormatAsTrackTime(value)}/{TimeUtils.FormatAsTrackTime(totalTimeSecs)} ({value / totalTimeSecs * 100:F0}%)");
	}
	
	public void LockMasterLabel(bool byPositionSeeker = false)
	{
		_masterLabelLocked = true;
		_masterLabelLockedByPositionSeeker = byPositionSeeker;
	}
	
	public void UnlockMasterLabel()
	{
		_masterLabelLocked = false;
		_masterLabelLockedByPositionSeeker = false;
	}
		
	private void NextTrack(bool autoplay = false)
	{
		var index = 0;
		if (_shuffleMode)
		{
			_randomizedTrackIndex += 1;
			if (_randomizedTrackIndex >= _playlist.Count)
			{
				_randomizedTrackIndex = _repeatMode ? 0 : _playlist.Count - 1;
			}
			index = _randomizedTrackIndices[_randomizedTrackIndex];
		}
		else
		{
			_currentTrackIndex += 1;
			if (_currentTrackIndex >= _playlist.Count)
			{
				_currentTrackIndex = _repeatMode ? 0 : _playlist.Count - 1;
			}
			index = _currentTrackIndex;
		}
		
		_trackPlayer.SetCurrentTrack(_playlist[index], autoplay || _trackPlayer.IsPlaying());
		_controls.Refresh();
	}
	
	private void PreviousTrack(bool autoplay = false)
	{
		var index = 0;
		if (_shuffleMode)
		{
			_randomizedTrackIndex -= 1;
			if (_randomizedTrackIndex < 0)
			{
				_randomizedTrackIndex = _repeatMode ? _playlist.Count - 1 : 0;
			}
			index = _randomizedTrackIndices[_randomizedTrackIndex];
		}
		else
		{
			_currentTrackIndex -= 1;
			if (_currentTrackIndex < 0)
			{
				_currentTrackIndex = _repeatMode ? _playlist.Count - 1 : 0;
			}
			index = _currentTrackIndex;
		}

		_trackPlayer.SetCurrentTrack(_playlist[index], autoplay || _trackPlayer.IsPlaying());
		_controls.Refresh();
	}

	private void ChangeToTrack(int index, bool autoplay = false)
	{
		ref var indexRef = ref _currentTrackIndex;
		if (_shuffleMode)
		{
			indexRef = ref _randomizedTrackIndex;
		}
		
		indexRef = index;
		_trackPlayer.SetCurrentTrack(_playlist[index], autoplay || _trackPlayer.IsPlaying());
		_controls.Refresh();
	}

	public void OnLoadTracksRequested()
	{
		FileDialog dialog = new();
		dialog.SetFileMode(FileDialog.FileModeEnum.OpenFiles);
		dialog.SetAccess(FileDialog.AccessEnum.Filesystem);
		dialog.SetFilters(CollectionsMarshal.AsSpan(
			AudioUtils.GetAllowedFileFilters()));
		dialog.SetUseNativeDialog(true);
		dialog.Connect(FileDialog.SignalName.FilesSelected, new Callable(this, nameof(LoadTracks)));
		dialog.Connect(AcceptDialog.SignalName.Canceled, new Callable(this, nameof(OnFileDialogClosed)));
		dialog.Connect(Window.SignalName.CloseRequested, new Callable(this, nameof(OnFileDialogClosed)));
		AddChild(dialog);
		dialog.PopupCenteredRatio();

		_lastUsedFileDialog = dialog;
	}

	public void LoadTracks(string[] paths)
	{
		_playlist.Clear();
		_playlist.AddRange(AudioUtils.LoadTracksFromPathList(paths));
		_playlist.Sort((a, b) => a.TrackNumber - b.TrackNumber);
		_trackPlayer.SetCurrentTrack(_playlist[0], false);
		_visualizer.Pause();
		_controls.Refresh();
		
		OnFileDialogClosed();
	}
	
	public void OnFileDialogClosed()
	{
		if (_lastUsedFileDialog == null)
			return;
		_lastUsedFileDialog.QueueFree();
		_lastUsedFileDialog = null;
	}
}