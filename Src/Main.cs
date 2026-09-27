using System;
using System.IO;
using System.Linq;
using GodAmp.Autoload;
using GodAmp.Components;
using GodAmp.Controls.Equalizer;
using GodAmp.Controls.MasterPanel;
using GodAmp.Controls.Playlist;
using GodAmp.Core;
using GodAmp.Data;
using GodAmp.Audio;
using GodAmp.Audio.Importing;
using GodAmp.Audio.Playback;
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
    [Export] private PlaybackController _playbackController = null!;
    [Export] private AudioImportController _audioImportController = null!;
    [Export] private WindowManager _windowManager = null!;

    private bool _masterLabelLocked = false;
    private bool _masterLabelLockedByPositionSeeker = false;

    private FileDialog? _lastUsedFileDialog = null;

    public override void _Ready()
    {
        _audioImportController.Restore(SettingsManager.Instance.GetLastPlaylistPath(), DefaultSongsPath);
        _masterPanel.RefreshModes();
        _visualizer.Pause();

        SignalBus.Instance.LockMasterLabel += LockMasterLabel;
        SignalBus.Instance.UnlockMasterLabel += UnlockMasterLabel;
        SignalBus.Instance.VolumeChanged += OnVolumeChanged;
        SignalBus.Instance.PannerBalanceChanged += OnPannerBalanceChanged;
        SignalBus.Instance.PositionSeekerChanged += OnPositionSeekerChanged;
        SignalBus.Instance.LoadTracksRequested += OnLoadTracksRequested;
        SignalBus.Instance.LoadTracksFromDirRequested += OnLoadTracksFromDirRequested;
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
        SignalBus.Instance.LockMasterLabel -= LockMasterLabel;
        SignalBus.Instance.UnlockMasterLabel -= UnlockMasterLabel;
        SignalBus.Instance.VolumeChanged -= OnVolumeChanged;
        SignalBus.Instance.PannerBalanceChanged -= OnPannerBalanceChanged;
        SignalBus.Instance.PositionSeekerChanged -= OnPositionSeekerChanged;
        SignalBus.Instance.LoadTracksRequested -= OnLoadTracksRequested;
        SignalBus.Instance.LoadTracksFromDirRequested -= OnLoadTracksFromDirRequested;
        SignalBus.Instance.LoadPlaylistRequested -= OnLoadPlaylistRequested;
        SignalBus.Instance.SavePlaylistRequested -= OnSavePlaylistRequested;
        SignalBus.Instance.ZoomModeRequested -= OnZoomModeRequested;
        SignalBus.Instance.ToggleEqualizerRequested -= OnToggleEqualizerRequested;
        SignalBus.Instance.TogglePlaylistRequested -= OnTogglePlaylistRequested;
        SignalBus.Instance.ToggleVisualizerRequested -= OnToggleVisualizerRequested;
        _windowManager.SaveWindowStates();
        SettingsManager.Instance.SaveAllSettings();
    }

    public override void _Process(double delta)
    {
        if (_playbackController.State == PlaybackState.Playing)
            _visualizer.Unpause();
        else
            _visualizer.Pause();

        if (!_masterLabelLocked)
        {
            if (_playbackController.CurrentEntry?.Track is { } currentTrack)
                _masterPanel.SetMasterLabelText(AudioUtils.GetFullTrackTitle(currentTrack, _playbackController.CurrentIndex + 1));
            else
                _masterPanel.SetMasterLabelText("");
        }
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
        if (!_playbackController.CanSeek)
            return;

        var totalTimeSecs = _playbackController.CurrentDuration;
        if (totalTimeSecs <= 0)
            return;
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

    /// <summary>Opens a replacement-track picker in response to Play on an empty queue.</summary>
    private void OnOpenTracksRequested() => OnLoadTracksRequested(true);

    /// <summary>Creates a filesystem picker starting in the platform's configured music directory.</summary>
    /// <param name="mode">Selection operation performed by the picker.</param>
    /// <returns>A native picker ready for filters, runtime callbacks, and scene attachment.</returns>
    private static FileDialog CreateMusicDialog(FileDialog.FileModeEnum mode)
    {
        string directory = OS.GetSystemDir(OS.SystemDir.Music);
        if (!Directory.Exists(directory))
            directory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var dialog = new FileDialog
        {
            FileMode = mode,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true
        };
        if (Directory.Exists(directory))
            dialog.CurrentDir = directory;
        SkinCursorController.UseSystemCursorFor(dialog);
        return dialog;
    }

    /// <summary>Opens a folder picker and binds its selection to the requested playlist operation.</summary>
    /// <param name="overridePlaylist">Whether selected tracks replace the playlist.</param>
    private void OnLoadTracksFromDirRequested(bool overridePlaylist = false)
    {
        FileDialog dialog = CreateMusicDialog(FileDialog.FileModeEnum.OpenDir);

        var dirSelectedCallback = Callable.From((string dirPath) => LoadTracksFromDirectory(dirPath, overridePlaylist));
        dialog.Connect(FileDialog.SignalName.DirSelected, dirSelectedCallback);
        dialog.Connect(AcceptDialog.SignalName.Canceled, new Callable(this, nameof(OnFileDialogClosed)));
        dialog.Connect(Window.SignalName.CloseRequested, new Callable(this, nameof(OnFileDialogClosed)));
        AddChild(dialog);
        dialog.PopupCenteredRatio();

        _lastUsedFileDialog = dialog;
    }

    /// <summary>Opens an audio picker and binds its selection to the requested playlist operation.</summary>
    /// <param name="overridePlaylist">Whether selected tracks replace the playlist.</param>
    private void OnLoadTracksRequested(bool overridePlaylist = false)
    {
        FileDialog dialog = CreateMusicDialog(FileDialog.FileModeEnum.OpenFiles);
        dialog.SetFilters(AudioFormats.FileFilters);
        var filesSelectedCallback = Callable.From((string[] paths) => LoadTracks(paths, overridePlaylist));
        dialog.Connect(FileDialog.SignalName.FilesSelected, filesSelectedCallback);
        dialog.Connect(AcceptDialog.SignalName.Canceled, new Callable(this, nameof(OnFileDialogClosed)));
        dialog.Connect(Window.SignalName.CloseRequested, new Callable(this, nameof(OnFileDialogClosed)));
        AddChild(dialog);
        dialog.PopupCenteredRatio();

        _lastUsedFileDialog = dialog;
    }

    /// <summary>Opens a playlist picker in the platform's music directory.</summary>
    private void OnLoadPlaylistRequested()
    {
        FileDialog dialog = CreateMusicDialog(FileDialog.FileModeEnum.OpenFile);
        dialog.SetFilters(["*.m3u; M3U Playlist", "*.m3u8; M3U8 Playlist"]);
        var fileSelectedCallback = Callable.From((string path) => LoadPlaylist(path));
        dialog.Connect(FileDialog.SignalName.FileSelected, fileSelectedCallback);
        dialog.Connect(AcceptDialog.SignalName.Canceled, new Callable(this, nameof(OnFileDialogClosed)));
        dialog.Connect(Window.SignalName.CloseRequested, new Callable(this, nameof(OnFileDialogClosed)));
        AddChild(dialog);
        dialog.PopupCenteredRatio();

        _lastUsedFileDialog = dialog;
    }

    /// <summary>Opens a playlist save picker in the platform's music directory.</summary>
    private void OnSavePlaylistRequested()
    {
        FileDialog dialog = CreateMusicDialog(FileDialog.FileModeEnum.SaveFile);
        dialog.SetFilters(["*.m3u; M3U Playlist", "*.m3u8; M3U8 Playlist"]);
        var fileSelectedCallback = Callable.From((string path) => SavePlaylist(path));
        dialog.Connect(FileDialog.SignalName.FileSelected, fileSelectedCallback);
        dialog.Connect(AcceptDialog.SignalName.Canceled, new Callable(this, nameof(OnFileDialogClosed)));
        dialog.Connect(Window.SignalName.CloseRequested, new Callable(this, nameof(OnFileDialogClosed)));
        AddChild(dialog);
        dialog.PopupCenteredRatio();

        _lastUsedFileDialog = dialog;
    }

    /// <summary>Writes queue source paths in visual order, including duplicate occurrences.</summary>
    /// <param name="path">Destination playlist path, with an optional M3U extension.</param>
    private void SavePlaylist(string path)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(ext))
            path += ".m3u";
        var absolutePaths = _playbackController.Entries
            .Select(entry => entry.Track.SourcePath)
            .Where(p => !string.IsNullOrWhiteSpace(p));
        M3UParser.Write(path, absolutePaths, relativePaths: false);
        OnFileDialogClosed();
    }

    /// <summary>Replaces playback only after importing a readable playlist or an intentionally empty list.</summary>
    /// <param name="path">Filesystem path to an M3U playlist.</param>
    private void LoadPlaylist(string path)
    {
        OnFileDialogClosed();
        _audioImportController.Enqueue(new ImportRequest(ImportSource.Playlist, [path], ImportMode.Replace));
    }

    private void LoadTracksFromDirectory(string directoryPath, bool overridePlaylist = false)
    {
        OnFileDialogClosed();
        _audioImportController.Enqueue(new ImportRequest(ImportSource.Folder, [directoryPath],
            overridePlaylist ? ImportMode.Replace : ImportMode.Append));
    }

    /// <summary>Imports audio files before applying an append or replacement to the queue owner.</summary>
    /// <param name="paths">Source audio paths selected by a picker.</param>
    /// <param name="overridePlaylist">Whether to replace the queue and load it stopped.</param>
    private void LoadTracks(string[] paths, bool overridePlaylist = false)
    {
        OnFileDialogClosed();
        _audioImportController.Enqueue(new ImportRequest(ImportSource.Files, paths,
            overridePlaylist ? ImportMode.Replace : ImportMode.Append));
    }

    public void OnFileDialogClosed()
    {
        if (_lastUsedFileDialog == null)
            return;
        _lastUsedFileDialog.QueueFree();
        _lastUsedFileDialog = null;
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

    /// <summary>Hides the EQ and synchronizes its visibility setting and menu controls.</summary>
    private void OnEqualizerCloseButtonClicked()
    {
        _equalizer.WindowRef.Hide();
        _masterPanel.ToggleEqualizerButton.ButtonPressed = false;
        _masterPanel.WinampMenuButton.SetEqualizerChecked(false);
        SettingsManager.Instance.SetWindowVisible(PlayerWindow.Equalizer, false);
    }

    /// <summary>Hides the playlist and synchronizes its visibility setting and menu controls.</summary>
    private void OnPlaylistCloseButtonClicked()
    {
        _playlist.WindowRef.Hide();
        _masterPanel.TogglePlaylistButton.ButtonPressed = false;
        _masterPanel.WinampMenuButton.SetPlaylistChecked(false);
        SettingsManager.Instance.SetWindowVisible(PlayerWindow.Playlist, false);
    }

    /// <summary>Hides the visualizer and synchronizes its visibility setting and menu entry.</summary>
    private void OnVisualizerCloseButtonClicked()
    {
        _visualizer.WindowRef.Hide();
        _masterPanel.WinampMenuButton.SetVisualizerChecked(false);
        SettingsManager.Instance.SetWindowVisible(PlayerWindow.Visualizer, false);
    }

    /// <summary>Toggles EQ visibility and synchronizes the setting and both menu controls.</summary>
    private void OnToggleEqualizerRequested()
    {
        _equalizer.WindowRef.Visible = !_equalizer.WindowRef.Visible;
        _masterPanel.ToggleEqualizerButton.ButtonPressed = _equalizer.WindowRef.Visible;
        _masterPanel.WinampMenuButton.SetEqualizerChecked(_equalizer.WindowRef.Visible);
        SettingsManager.Instance.SetWindowVisible(PlayerWindow.Equalizer, _equalizer.WindowRef.Visible);
    }

    /// <summary>Toggles playlist visibility and synchronizes the setting and both menu controls.</summary>
    private void OnTogglePlaylistRequested()
    {
        _playlist.WindowRef.Visible = !_playlist.WindowRef.Visible;
        _masterPanel.TogglePlaylistButton.ButtonPressed = _playlist.WindowRef.Visible;
        _masterPanel.WinampMenuButton.SetPlaylistChecked(_playlist.WindowRef.Visible);
        SettingsManager.Instance.SetWindowVisible(PlayerWindow.Playlist, _playlist.WindowRef.Visible);
    }

    /// <summary>Toggles visualizer visibility and synchronizes the setting and menu entry.</summary>
    private void OnToggleVisualizerRequested()
    {
        _visualizer.WindowRef.Visible = !_visualizer.WindowRef.Visible;
        _masterPanel.WinampMenuButton.SetVisualizerChecked(_visualizer.WindowRef.Visible);
        SettingsManager.Instance.SetWindowVisible(PlayerWindow.Visualizer, _visualizer.WindowRef.Visible);
    }
}
