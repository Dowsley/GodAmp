using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using GodAmp.Autoload;
using GodAmp.Controls.Equalizer;
using GodAmp.Controls.MasterPanel;
using GodAmp.Controls.Playlist;
using GodAmp.Data;
using GodAmp.Player;
using GodAmp.Utils;
using Godot;

namespace GodAmp;

public partial class Main : HBoxContainer
{
	[ExportGroup("Config")]
	[Export] public string DefaultSongsPath;
	
	private MasterPanel _masterPanel;
	private Equalizer _equalizer;
	private Playlist _playlist;
	private Visualizer.Visualizer _visualizer;
	private TrackPlayer _trackPlayer;

	private List<Track> _trackPlaylist;
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
		_masterPanel = GetNode<MasterPanel>("%MasterPanel");
		_equalizer = GetNode<Equalizer>("%Equalizer");
		_playlist = GetNode<Playlist>("%Playlist");
		_visualizer = GetNode<Visualizer.Visualizer>("%Visualizer");
		_trackPlayer = GetNode<TrackPlayer>("%TrackPlayer");

		_trackPlaylist = AudioUtils.LoadAllTracksFromDir(DefaultSongsPath);
		if (_trackPlaylist.Count == 0)
		{
			GD.PrintErr("No tracks found in the default songs path.");
		}
		_trackPlaylist.Sort((a, b) => a.TrackNumber - b.TrackNumber);
		_trackPlayer.SetCurrentTrack(_trackPlaylist[_currentTrackIndex], false);
		_visualizer.Pause();
		
		_masterPanel.ToggleEqualizerRequested += OnToggleEqualizerRequested;
		_masterPanel.TogglePlaylistRequested += OnTogglePlaylistRequested;
		_masterPanel.Setup(_trackPlayer);
		_playlist.Setup(_trackPlayer, _trackPlaylist);
		_masterPanel.Refresh();
		_playlist.Refresh();
		
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
			_masterPanel.SetMasterLabelText(AudioUtils.GetFullTrackTitle(_trackPlayer.CurrentTrack));
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
			_randomizedTrackIndices = Enumerable.Range(0, _trackPlaylist.Count).OrderBy(_ => random.Next()).ToList();
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
		_masterPanel.SetMasterLabelText($"VOLUME: {Convert.ToInt64(volume * 100)}%");
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
		_masterPanel.SetMasterLabelText(text);
	}
	
	public void OnPositionSeekerChanged(float value)
	{
		var totalTimeSecs = _trackPlayer.CurrentTrack.Duration;
		if (_masterLabelLocked && _masterLabelLockedByPositionSeeker)
			_masterPanel.SetMasterLabelText(
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
			if (_randomizedTrackIndex >= _trackPlaylist.Count)
			{
				_randomizedTrackIndex = _repeatMode ? 0 : _trackPlaylist.Count - 1;
			}
			index = _randomizedTrackIndices[_randomizedTrackIndex];
		}
		else
		{
			_currentTrackIndex += 1;
			if (_currentTrackIndex >= _trackPlaylist.Count)
			{
				_currentTrackIndex = _repeatMode ? 0 : _trackPlaylist.Count - 1;
			}
			index = _currentTrackIndex;
		}
		
		_trackPlayer.SetCurrentTrack(_trackPlaylist[index], autoplay || _trackPlayer.IsPlaying());
		_masterPanel.Refresh();
		_playlist.Refresh();
	}
	
	private void PreviousTrack(bool autoplay = false)
	{
		var index = 0;
		if (_shuffleMode)
		{
			_randomizedTrackIndex -= 1;
			if (_randomizedTrackIndex < 0)
			{
				_randomizedTrackIndex = _repeatMode ? _trackPlaylist.Count - 1 : 0;
			}
			index = _randomizedTrackIndices[_randomizedTrackIndex];
		}
		else
		{
			_currentTrackIndex -= 1;
			if (_currentTrackIndex < 0)
			{
				_currentTrackIndex = _repeatMode ? _trackPlaylist.Count - 1 : 0;
			}
			index = _currentTrackIndex;
		}

		_trackPlayer.SetCurrentTrack(_trackPlaylist[index], autoplay || _trackPlayer.IsPlaying());
		_masterPanel.Refresh();
		_playlist.Refresh();
	}

	private void ChangeToTrack(int index, bool autoplay = false)
	{
		ref var indexRef = ref _currentTrackIndex;
		if (_shuffleMode)
		{
			indexRef = ref _randomizedTrackIndex;
		}
		
		indexRef = index;
		_trackPlayer.SetCurrentTrack(_trackPlaylist[index], autoplay || _trackPlayer.IsPlaying());
		_masterPanel.Refresh();
		_playlist.Refresh();
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
		_trackPlaylist.Clear();
		_trackPlaylist.AddRange(AudioUtils.LoadTracksFromPathList(paths));
		_trackPlaylist.Sort((a, b) => a.TrackNumber - b.TrackNumber);
		_trackPlayer.SetCurrentTrack(_trackPlaylist[0], false);
		_visualizer.Pause();
		_masterPanel.Refresh();
		_playlist.Refresh();
		
		OnFileDialogClosed();
	}
	
	public void OnFileDialogClosed()
	{
		if (_lastUsedFileDialog == null)
			return;
		_lastUsedFileDialog.QueueFree();
		_lastUsedFileDialog = null;
	}
	
	public void OnToggleEqualizerRequested()
	{
		_equalizer.Visible = !_equalizer.Visible;
	}

	public void OnTogglePlaylistRequested()
	{
		_playlist.Visible = !_playlist.Visible;
	}
	
	public void OnEqualizerCloseButtonClicked()
	{
		_masterPanel.ToggleEqualizerButton.ButtonPressed = false;
	}

	public void OnPlaylistCloseButtonClicked()
	{
		_masterPanel.TogglePlaylistButton.ButtonPressed = false;
	}
}