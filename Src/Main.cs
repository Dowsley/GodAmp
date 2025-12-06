using System;
using System.Collections.Generic;
using System.IO;
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
	
	[ExportGroup("References")]
	[Export] private MasterPanel _masterPanel;
	[Export] private Equalizer _equalizer;
	[Export] private Playlist _playlist;
	[Export] private Visualizer.Visualizer _visualizer;
	[Export] private TrackPlayer _trackPlayer;
	[Export] private Window _equalizerWindow;
	[Export] private Window _playlistWindow;
	[Export] private Window _visualizerWindow;

	private List<Track> _trackPlaylist;
	private int _currentTrackIndex = 0;

	private bool _repeatMode = false;
	private bool _shuffleMode = false;
	private int _randomizedTrackIndex = 0;
	private List<int> _randomizedTrackIndices = [];
	private bool _masterLabelLocked = false;
	private bool _masterLabelLockedByPositionSeeker = false;

	private FileDialog _lastUsedFileDialog;
	private Vector2I _originalWindowSize;
	private Vector2I _originalVisualizerWindowSize;
	private bool _bringingWindowsToForeground;
	
	public override void _Ready()
	{
		int width = (int)ProjectSettings.GetSetting("display/window/size/viewport_width");
		int height = (int)ProjectSettings.GetSetting("display/window/size/viewport_height");
		_originalWindowSize = new Vector2I(width, height);
		_originalVisualizerWindowSize = _visualizerWindow.Size;

		GetWindow().AlwaysOnTop = true; // Keep main window on top to prevent occlusion-based throttling
		CenterWindow();

		// Try to load the last used playlist, fall back to default songs path
		string lastPlaylistPath = SettingsManager.Instance.GetLastPlaylistPath();
		if (!string.IsNullOrWhiteSpace(lastPlaylistPath) && File.Exists(lastPlaylistPath))
		{
			try
			{
				var resolved = M3UParser.Parse(lastPlaylistPath);
				_trackPlaylist = AudioUtils.LoadTracksFromPathList(resolved);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"Failed to load last playlist: {ex.Message}. Falling back to default songs path.");
				_trackPlaylist = AudioUtils.LoadAllTracksFromDir(DefaultSongsPath);
			}
		}
		else
		{
			_trackPlaylist = AudioUtils.LoadAllTracksFromDir(DefaultSongsPath);
		}

		if (_trackPlaylist.Count == 0)
		{
			GD.PrintErr("No tracks found.");
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
		SignalBus.Instance.LoadTracksFromDirRequested += OnLoadTracksFromDirRequested;
		SignalBus.Instance.RemoveSelectedTracksFromPlaylistRequested += RemoveSelectedTracksFromPlaylist;
		SignalBus.Instance.RemoveAllTracksFromPlaylistRequested += RemoveAllTracksFromPlaylist;
		SignalBus.Instance.CropPlaylistRequested += CropPlaylist;
		SignalBus.Instance.LoadPlaylistRequested += OnLoadPlaylistRequested;
		SignalBus.Instance.SavePlaylistRequested += OnSavePlaylistRequested;
		SignalBus.Instance.ZoomModeRequested += OnZoomModeRequested;

		GetWindow().FocusEntered += OnMainWindowFocused;

		LoadSettingsState();
	}

	public override void _ExitTree()
	{
		SettingsManager.Instance.SaveAllSettings();
	}

	public override void _Process(double delta)
	{
		if (_trackPlayer.IsPlaying())
			_visualizer.Unpause();
		else
			_visualizer.Pause();
		
		if (!_masterLabelLocked)
		{
			_masterPanel.SetMasterLabelText(AudioUtils.GetFullTrackTitle(_trackPlayer.CurrentTrack, _currentTrackIndex + 1));
		}
	}

	private void OnNextTrackRequested()
	{
		NextTrack();
	}

	private void OnPreviousTrackRequested()
	{
		PreviousTrack();
	}
	
	private void OnTrackPlayerFinished()
	{
		NextTrack(true);
	}

	private void OnShuffleModeRequested()
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

	private void OnRepeatModeRequested()
	{
		_repeatMode = !_repeatMode;
	}

	private void OnChangeToTrackRequested(int index)
	{
		ChangeToTrack(index, true);
	}

	private void OnVolumeChanged(float volume)
	{
		_masterPanel.SetMasterLabelText($"VOLUME: {Convert.ToInt64(volume * 100)}%");
	}
	
	private void OnPannerBalanceChanged(float value)
	{
		string text;
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
	
	private void OnPositionSeekerChanged(float value)
	{
		var totalTimeSecs = _trackPlayer.CurrentTrack.Duration;
		if (_masterLabelLocked && _masterLabelLockedByPositionSeeker)
			_masterPanel.SetMasterLabelText(
				$"SEEK TO: {TimeUtils.FormatAsTrackTime(value)}/{TimeUtils.FormatAsTrackTime(totalTimeSecs)} ({value / totalTimeSecs * 100:F0}%)");
	}
	
	private void LockMasterLabel(bool byPositionSeeker = false)
	{
		_masterLabelLocked = true;
		_masterLabelLockedByPositionSeeker = byPositionSeeker;
	}
	
	private void UnlockMasterLabel()
	{
		_masterLabelLocked = false;
		_masterLabelLockedByPositionSeeker = false;
	}
		
	private void NextTrack(bool autoplay = false)
	{
		int index;
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

		if (!(index < _trackPlaylist.Count && index >= 0))
			return;
		
		_trackPlayer.SetCurrentTrack(_trackPlaylist[index], autoplay || _trackPlayer.IsPlaying());
		_masterPanel.Refresh();
		_playlist.Refresh();
	}
	
	private void PreviousTrack(bool autoplay = false)
	{
		int index;
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
		
		if (!(index < _trackPlaylist.Count && index >= 0))
			return;

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
	
	private void OnLoadTracksFromDirRequested(bool overridePlaylist = false)
	{
		FileDialog dialog = new();
		dialog.SetFileMode(FileDialog.FileModeEnum.OpenDir);
		dialog.SetAccess(FileDialog.AccessEnum.Filesystem);
		dialog.SetUseNativeDialog(true);
		
		// Connect to DirSelected instead of FilesSelected
		var dirSelectedCallback = Callable.From((string dirPath) => LoadTracksFromDirectory(dirPath, overridePlaylist));
		dialog.Connect(FileDialog.SignalName.DirSelected, dirSelectedCallback);
		dialog.Connect(AcceptDialog.SignalName.Canceled, new Callable(this, nameof(OnFileDialogClosed)));
		dialog.Connect(Window.SignalName.CloseRequested, new Callable(this, nameof(OnFileDialogClosed)));
		AddChild(dialog);
		dialog.PopupCenteredRatio();

		_lastUsedFileDialog = dialog;
	}

	private void OnLoadTracksRequested(bool overridePlaylist = false)
	{
		FileDialog dialog = new();
		dialog.SetFileMode(FileDialog.FileModeEnum.OpenFiles);
		dialog.SetAccess(FileDialog.AccessEnum.Filesystem);
		dialog.SetFilters(CollectionsMarshal.AsSpan(
			AudioUtils.GetAllowedFileFilters()));
		dialog.SetUseNativeDialog(true);
		var filesSelectedCallback = Callable.From((string[] paths) => LoadTracks(paths, overridePlaylist));
		dialog.Connect(FileDialog.SignalName.FilesSelected, filesSelectedCallback);
		dialog.Connect(AcceptDialog.SignalName.Canceled, new Callable(this, nameof(OnFileDialogClosed)));
		dialog.Connect(Window.SignalName.CloseRequested, new Callable(this, nameof(OnFileDialogClosed)));
		AddChild(dialog);
		dialog.PopupCenteredRatio();

		_lastUsedFileDialog = dialog;
	}

	private void OnLoadPlaylistRequested()
	{
		FileDialog dialog = new();
		dialog.SetFileMode(FileDialog.FileModeEnum.OpenFile);
		dialog.SetAccess(FileDialog.AccessEnum.Filesystem);
		dialog.SetFilters(["*.m3u; M3U Playlist","*.m3u8; M3U8 Playlist"]);
		dialog.SetUseNativeDialog(true);
		var fileSelectedCallback = Callable.From((string path) => LoadPlaylist(path));
		dialog.Connect(FileDialog.SignalName.FileSelected, fileSelectedCallback);
		dialog.Connect(AcceptDialog.SignalName.Canceled, new Callable(this, nameof(OnFileDialogClosed)));
		dialog.Connect(Window.SignalName.CloseRequested, new Callable(this, nameof(OnFileDialogClosed)));
		AddChild(dialog);
		dialog.PopupCenteredRatio();

		_lastUsedFileDialog = dialog;
	}

	private void OnSavePlaylistRequested()
	{
		FileDialog dialog = new();
		dialog.SetFileMode(FileDialog.FileModeEnum.SaveFile);
		dialog.SetAccess(FileDialog.AccessEnum.Filesystem);
		dialog.SetFilters(["*.m3u; M3U Playlist","*.m3u8; M3U8 Playlist"]);
		dialog.SetUseNativeDialog(true);
		var fileSelectedCallback = Callable.From((string path) => SavePlaylist(path));
		dialog.Connect(FileDialog.SignalName.FileSelected, fileSelectedCallback);
		dialog.Connect(AcceptDialog.SignalName.Canceled, new Callable(this, nameof(OnFileDialogClosed)));
		dialog.Connect(Window.SignalName.CloseRequested, new Callable(this, nameof(OnFileDialogClosed)));
		AddChild(dialog);
		dialog.PopupCenteredRatio();

		_lastUsedFileDialog = dialog;
	}

	private void SavePlaylist(string path)
	{
		var ext = Path.GetExtension(path);
		if (string.IsNullOrWhiteSpace(ext))
			path += ".m3u";
		var absolutePaths = _trackPlaylist
			.Select(t => t?.SourcePath)
			.Where(p => !string.IsNullOrWhiteSpace(p));
		M3UParser.Write(path, absolutePaths, relativePaths: false);
		OnFileDialogClosed();
	}

	private void LoadPlaylist(string path)
	{
		var resolved = M3UParser.Parse(path);
		var tracks = AudioUtils.LoadTracksFromPathList(resolved);
		_trackPlaylist.Clear();
		_trackPlaylist.AddRange(tracks);
		if (_trackPlaylist.Count > 0)
			_trackPlayer.SetCurrentTrack(_trackPlaylist[0], false);
		_visualizer.Pause();
		_masterPanel.Refresh();
		_playlist.Refresh();

		// Save the last loaded playlist path
		SettingsManager.Instance.SetLastPlaylistPath(path);

		OnFileDialogClosed();
	}
	
	private void LoadTracksFromDirectory(string directoryPath, bool overridePlaylist = false)
	{
		var audioFiles = GetAudioFilesFromDirectory(directoryPath);
		if (audioFiles.Length == 0)
		{
			GD.Print("No audio files found in directory");
			OnFileDialogClosed();
			return;
		}
    
		LoadTracks(audioFiles, overridePlaylist);
	}

	private void RemoveAllTracksFromPlaylist()
	{
		_trackPlaylist.Clear();
		_playlist.Refresh();
	}

	private void CropPlaylist()
	{
		var selected = _playlist.GetSelectedIndices();
		// avoid index shifts
		for (int i = _trackPlaylist.Count - 1; i >= 0; i--)
		{
			if (!selected.Contains(i))
				_trackPlaylist.RemoveAt(i);
		}
		_playlist.Refresh();
	}
	
	private void RemoveSelectedTracksFromPlaylist()
	{
		var selected = _playlist.GetSelectedIndices();
		// avoid index shifts
		foreach (var i in selected.OrderByDescending(x => x))
		{
			_trackPlaylist.RemoveAt(i);
		}
		_playlist.Refresh();
	}

	private static string[] GetAudioFilesFromDirectory(string directoryPath)
	{
		var audioFiles = new List<string>();
		var allowedExtensions = AudioUtils.GetAllowedFileExtensions();
		using var dir = DirAccess.Open(directoryPath);

		if (dir == null)
			return [];

		dir.ListDirBegin();
		string fileName = dir.GetNext();

		while (fileName != "")
		{
			if (!dir.CurrentIsDir())
			{
				string extension = $".{fileName.GetExtension().ToLower()}";
				if (allowedExtensions.Contains(extension))
				{
					audioFiles.Add(Path.Combine(directoryPath, fileName));
				}
			}
			fileName = dir.GetNext();
		}

		return audioFiles.ToArray();
	}

	private void LoadTracks(string[] paths, bool overridePlaylist = false)
	{
		if (overridePlaylist)
			_trackPlaylist.Clear();
		var tracks = AudioUtils.LoadTracksFromPathList(paths);
		tracks.Sort((a, b) => a.TrackNumber - b.TrackNumber);
		_trackPlaylist.AddRange(tracks);
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
	
	private void OnToggleEqualizerRequested()
	{
		_equalizer.Visible = !_equalizer.Visible;
	}

	private void OnTogglePlaylistRequested()
	{
		_playlist.Visible = !_playlist.Visible;
	}

	private void LoadSettingsState()
	{
		float savedVolume = SettingsManager.Instance.GetVolume();
		_masterPanel.SetVolumeValue(savedVolume);
		_trackPlayer.VolumeLinear = savedVolume;

		int savedZoomMode = SettingsManager.Instance.GetZoomMode();
		int multiplier = savedZoomMode switch
		{
			0 => 1,
			1 => 2,
			_ => savedZoomMode
		};
		SetZoomMode(multiplier);
	}

	private void OnZoomModeRequested(int multiplier)
	{
		SetZoomMode(multiplier);
	}

	private void SetZoomMode(int multiplier)
	{
		var scale = new Vector2(multiplier, multiplier);

		var newSize = _originalWindowSize * multiplier;
		GetWindow().Size = newSize;
		
		_equalizerWindow.Size = newSize;
		_equalizer.Size = _originalWindowSize;
		_equalizer.Scale = scale;
		
		_playlistWindow.Size = newSize;
		_playlist.Size = _originalWindowSize;
		_playlist.Scale = scale;
		
		_visualizerWindow.Size = _originalVisualizerWindowSize * multiplier;
		_visualizer.Size = _originalVisualizerWindowSize;
		_visualizer.Scale = scale;
		
		SettingsManager.Instance.SetZoomMode(multiplier);
		CenterWindow();
	}
	
	private void CenterWindow()
	{
		var screenSize = DisplayServer.ScreenGetSize();
		var windowSize = GetWindow().Size;
		var centeredPosition = (screenSize - windowSize) / 2;
		DisplayServer.WindowSetPosition(centeredPosition);
	}

	private void OnEqualizerCloseButtonClicked()
	{
		_masterPanel.ToggleEqualizerButton.ButtonPressed = false;
	}

	private void OnPlaylistCloseButtonClicked()
	{
		_masterPanel.TogglePlaylistButton.ButtonPressed = false;
	}

	private void OnMainWindowFocused()
	{
		if (_bringingWindowsToForeground)
			return;
		
		_bringingWindowsToForeground = true;
		
		// Order matters here. We bring all windows to the front, and the main window last to keep focus.
		_equalizerWindow.GrabFocus();
		_playlistWindow.GrabFocus();
		_visualizerWindow.GrabFocus();
		GetWindow().GrabFocus();
		
		_bringingWindowsToForeground = false;
	}
}