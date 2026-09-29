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
using GodAmp.Audio.Playlists;
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
    [Export] private PlaybackController _playbackController = null!;
    [Export] private AudioImportController _audioImportController = null!;
    [Export] private WindowManager _windowManager = null!;

    private FileDialog? _lastUsedFileDialog = null;

    public override void _Ready()
    {
        /* The engine creates the root window outside Main.tscn. */
        GetWindow().FilesDropped += OnFilesDropped;
        _audioImportController.Restore(SettingsManager.Instance.GetLastPlaylistPath(), DefaultSongsPath);
        _masterPanel.RefreshModes();
        _visualizer.Pause();

        LoadSettingsState();
    }

    public override void _ExitTree()
    {
        GetWindow().FilesDropped -= OnFilesDropped;
        _windowManager.SaveWindowStates();
        SettingsManager.Instance.SaveAllSettings();
    }

    /// <summary>Reflects transport changes in the visualizer without polling static state each frame.</summary>
    private void OnPlaybackStateChanged()
    {
        if (_playbackController.State == PlaybackState.Playing)
            _visualizer.Unpause();
        else
            _visualizer.Pause();

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
        dialog.SetFilters([.. AudioFormats.FileFilters, .. PlaylistFile.FileFilters]);
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
        dialog.SetFilters(PlaylistFile.FileFilters);
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
        dialog.SetFilters(PlaylistFile.FileFilters);
        var fileSelectedCallback = Callable.From((string path) => SavePlaylist(path));
        dialog.Connect(FileDialog.SignalName.FileSelected, fileSelectedCallback);
        dialog.Connect(AcceptDialog.SignalName.Canceled, new Callable(this, nameof(OnFileDialogClosed)));
        dialog.Connect(Window.SignalName.CloseRequested, new Callable(this, nameof(OnFileDialogClosed)));
        AddChild(dialog);
        dialog.PopupCenteredRatio();

        _lastUsedFileDialog = dialog;
    }

    /// <summary>Saves queue order and display metadata, retaining duplicate occurrences.</summary>
    /// <param name="path">Playlist destination; a missing extension defaults to M3U8.</param>
    private void SavePlaylist(string path)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(ext))
            path += ".m3u8";
        var entries = _playbackController.Entries.Select(entry => new PlaylistEntry(entry.Track.SourcePath,
            AudioUtils.GetTrackTitle(entry.Track), entry.Track.Duration > 0 ? entry.Track.Duration : null));
        try
        {
            PlaylistFile.Write(path, entries);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            _audioImportController.ReportFileFailure(path, AudioIssueKind.Access, exception.Message);
        }
        OnFileDialogClosed();
    }

    /// <summary>Replaces playback only after importing a readable playlist or an intentionally empty list.</summary>
    /// <param name="path">Filesystem path to an M3U, M3U8 or PLS playlist.</param>
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

    /// <summary>Appends a mixed OS drop without interrupting the current track.</summary>
    /// <param name="paths">Files, playlists and directories provided by the native window.</param>
    private void OnFilesDropped(string[] paths) =>
        _audioImportController.Enqueue(new ImportRequest(ImportSource.Files, paths, ImportMode.Append));

    /// <inheritdoc />
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (_playlist.HandleJumpShortcut(@event))
        {
            GetViewport().SetInputAsHandled();
        }
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
