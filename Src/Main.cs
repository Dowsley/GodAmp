using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using GodAmp.Autoload;
using GodAmp.Components;
using GodAmp.Controls.Equalizer;
using GodAmp.Controls.MasterPanel;
using GodAmp.Controls.Playlist;
using GodAmp.Core;
using GodAmp.Data;
using GodAmp.Utils;
using Godot;

namespace GodAmp;

public partial class Main : HBoxContainer
{
    [ExportGroup("Config")]
    [Export] public string DefaultSongsPath = null!;

    [ExportGroup("References")]
    [Export] private MasterPanel _masterPanel = null!;
    [Export] private Equalizer _equalizer = null!;
    [Export] private Playlist _playlist = null!;
    [Export] private Visualizer.Visualizer _visualizer = null!;
    [Export] private TrackPlayer _trackPlayer = null!;
    [Export] private WindowManager _windowManager = null!;

    private Window _masterPanelWindow = null!;
    private WindowPanelContainer? _windowContainerBeingDragged = null;
    private bool _grabbingFocusLock = false;

    private List<Track> _trackPlaylist = null!;
    private int _currentTrackIndex = 0;

    private bool _repeatMode = false;
    private bool _shuffleMode = false;
    private int _randomizedTrackIndex = 0;
    private List<int> _randomizedTrackIndices = [];
    private bool _masterLabelLocked = false;
    private bool _masterLabelLockedByPositionSeeker = false;

    private FileDialog? _lastUsedFileDialog = null;

    public override void _Ready()
    {
        _trackPlaylist = LoadInitialPlaylist();

        if (_trackPlaylist.Count > 0)
        {
            _trackPlaylist.Sort((a, b) => a.TrackNumber - b.TrackNumber);
            _trackPlayer.SetCurrentTrack(_trackPlaylist[_currentTrackIndex], false);
        }
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
        SignalBus.Instance.ToggleEqualizerRequested += OnToggleEqualizerRequested;
        SignalBus.Instance.TogglePlaylistRequested += OnTogglePlaylistRequested;
        SignalBus.Instance.ToggleVisualizerRequested += OnToggleVisualizerRequested;

        LoadSettingsState();
    }

    public override void _ExitTree()
    {
        _windowManager.SaveWindowStates();
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
            if (_trackPlayer.CurrentTrack is { } currentTrack)
                _masterPanel.SetMasterLabelText(AudioUtils.GetFullTrackTitle(currentTrack, _currentTrackIndex + 1));
            else
                _masterPanel.SetMasterLabelText("");
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
        if (_trackPlayer.CurrentTrack is not { } track)
            return;

        var totalTimeSecs = track.Duration;
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
        dialog.SetFilters(["*.m3u; M3U Playlist", "*.m3u8; M3U8 Playlist"]);
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
        dialog.SetFilters(["*.m3u; M3U Playlist", "*.m3u8; M3U8 Playlist"]);
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
            .Select(t => t.SourcePath)
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
        ClearCurrentTrackIfPlaylistEmpty();
        _playlist.Refresh();
    }

    private void CropPlaylist()
    {
        var selected = _playlist.GetSelectedIndices();
        for (int i = _trackPlaylist.Count - 1; i >= 0; i--)
        {
            if (!selected.Contains(i))
                _trackPlaylist.RemoveAt(i);
        }
        ClearCurrentTrackIfPlaylistEmpty();
        _playlist.Refresh();
    }

    private void RemoveSelectedTracksFromPlaylist()
    {
        var selected = _playlist.GetSelectedIndices();
        foreach (var i in selected.OrderByDescending(x => x))
        {
            _trackPlaylist.RemoveAt(i);
        }
        ClearCurrentTrackIfPlaylistEmpty();
        _playlist.Refresh();
    }

    private void ClearCurrentTrackIfPlaylistEmpty()
    {
        if (_trackPlaylist.Count == 0)
        {
            _trackPlayer.ClearCurrentTrack();
            _masterPanel.Refresh();
        }
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
        var tracks = AudioUtils.LoadTracksFromPathList(paths);
        if (tracks.Count == 0)
        {
            OnFileDialogClosed();
            return;
        }

        if (overridePlaylist)
            _trackPlaylist.Clear();
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

    private List<Track> LoadInitialPlaylist()
    {
        string lastPlaylistPath = SettingsManager.Instance.GetLastPlaylistPath();
        if (!string.IsNullOrWhiteSpace(lastPlaylistPath) && File.Exists(lastPlaylistPath))
        {
            try
            {
                var resolved = M3UParser.Parse(lastPlaylistPath);
                var tracks = AudioUtils.LoadTracksFromPathList(resolved);
                if (tracks.Count > 0)
                    return tracks;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to load last playlist: {ex.Message}");
            }
        }
        return AudioUtils.LoadAllTracksFromDir(DefaultSongsPath);
    }

    private void LoadSettingsState()
    {
        float savedVolume = SettingsManager.Instance.GetVolume();
        _masterPanel.SetVolumeValue(savedVolume);
        _trackPlayer.VolumeLinear = savedVolume;

        var multiplier = SettingsManager.Instance.GetZoomMode();
        _windowManager.SetZoomMode(multiplier);
    }

    private void OnZoomModeRequested(int multiplier)
    {
        _windowManager.SetZoomMode(multiplier);
    }

    private void OnEqualizerCloseButtonClicked()
    {
        _equalizer.WindowRef.Hide();
        _masterPanel.ToggleEqualizerButton.ButtonPressed = false;
        _masterPanel.WinampMenuButton.SetEqualizerChecked(false);
        SettingsManager.Instance.SetWindowVisible("equalizer", false);
    }

    private void OnPlaylistCloseButtonClicked()
    {
        _playlist.WindowRef.Hide();
        _masterPanel.TogglePlaylistButton.ButtonPressed = false;
        _masterPanel.WinampMenuButton.SetPlaylistChecked(false);
        SettingsManager.Instance.SetWindowVisible("playlist", false);
    }

    private void OnVisualizerCloseButtonClicked()
    {
        _visualizer.WindowRef.Hide();
        _masterPanel.WinampMenuButton.SetVisualizerChecked(false);
        SettingsManager.Instance.SetWindowVisible("visualizer", false);
    }

    private void OnToggleEqualizerRequested()
    {
        _equalizer.WindowRef.Visible = !_equalizer.WindowRef.Visible;
        _masterPanel.ToggleEqualizerButton.ButtonPressed = _equalizer.WindowRef.Visible;
        _masterPanel.WinampMenuButton.SetEqualizerChecked(_equalizer.WindowRef.Visible);
        SettingsManager.Instance.SetWindowVisible("equalizer", _equalizer.WindowRef.Visible);
    }

    private void OnTogglePlaylistRequested()
    {
        _playlist.WindowRef.Visible = !_playlist.WindowRef.Visible;
        _masterPanel.TogglePlaylistButton.ButtonPressed = _playlist.WindowRef.Visible;
        _masterPanel.WinampMenuButton.SetPlaylistChecked(_playlist.WindowRef.Visible);
        SettingsManager.Instance.SetWindowVisible("playlist", _playlist.WindowRef.Visible);
    }

    private void OnToggleVisualizerRequested()
    {
        _visualizer.WindowRef.Visible = !_visualizer.WindowRef.Visible;
        _masterPanel.WinampMenuButton.SetVisualizerChecked(_visualizer.WindowRef.Visible);
        SettingsManager.Instance.SetWindowVisible("visualizer", _visualizer.WindowRef.Visible);
    }
}