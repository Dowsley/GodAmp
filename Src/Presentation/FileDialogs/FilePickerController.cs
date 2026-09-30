using System.IO;
using GodAmp.Media.Sources;
using GodAmp.Presentation.Components;
using GodAmp.Playlists;
using Godot;

namespace GodAmp.Presentation.FileDialogs;

/// <summary>Owns temporary native pickers and publishes selections to scene-wired feature owners.</summary>
public partial class FilePickerController : Node
{
    [Signal] public delegate void FilesSelectedEventHandler(string[] paths, bool replace);
    [Signal] public delegate void FolderSelectedEventHandler(string path, bool replace);
    [Signal] public delegate void PlaylistSelectedEventHandler(string path);
    [Signal] public delegate void PlaylistDestinationSelectedEventHandler(string path);

    [Export] private PackedScene _files = null!;
    [Export] private PackedScene _folder = null!;
    [Export] private PackedScene _playlist = null!;
    [Export] private PackedScene _savePlaylist = null!;
    private FileDialog? _active;

    /// <summary>Opens the replacement picker when playback has no queued tracks.</summary>
    public void OpenReplacementTracks() => OpenFiles(true);

    /// <summary>Opens a picker for audio files and playlists.</summary>
    /// <param name="replace">Whether the selection replaces the playback queue.</param>
    public void OpenFiles(bool replace = false)
    {
        FileDialog dialog = Prepare(_files);
        dialog.SetFilters([.. AudioFormats.FileFilters, .. PlaylistFile.FileFilters]);
        dialog.FilesSelected += paths =>
        {
            Close();
            EmitSignal(SignalName.FilesSelected, paths, replace);
        };
        dialog.PopupCenteredRatio();
    }

    /// <summary>Opens a folder picker for recursive media discovery.</summary>
    /// <param name="replace">Whether the selection replaces the playback queue.</param>
    public void OpenFolder(bool replace = false)
    {
        FileDialog dialog = Prepare(_folder);
        dialog.DirSelected += path =>
        {
            Close();
            EmitSignal(SignalName.FolderSelected, path, replace);
        };
        dialog.PopupCenteredRatio();
    }

    /// <summary>Opens a playlist source picker.</summary>
    public void OpenPlaylist()
    {
        FileDialog dialog = Prepare(_playlist);
        dialog.SetFilters(PlaylistFile.FileFilters);
        dialog.FileSelected += path =>
        {
            Close();
            EmitSignal(SignalName.PlaylistSelected, path);
        };
        dialog.PopupCenteredRatio();
    }

    /// <summary>Opens a playlist destination picker.</summary>
    public void SavePlaylist()
    {
        FileDialog dialog = Prepare(_savePlaylist);
        dialog.SetFilters(PlaylistFile.FileFilters);
        dialog.FileSelected += path =>
        {
            Close();
            EmitSignal(SignalName.PlaylistDestinationSelected, path);
        };
        dialog.PopupCenteredRatio();
    }

    private FileDialog Prepare(PackedScene scene)
    {
        Close();
        FileDialog dialog = scene.Instantiate<FileDialog>();
        string directory = OS.GetSystemDir(OS.SystemDir.Music);
        if (!Directory.Exists(directory))
            directory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (Directory.Exists(directory))
            dialog.CurrentDir = directory;
        /* Each request owns a fresh picker; its callbacks end with that instance. */
        dialog.Canceled += Close;
        dialog.CloseRequested += Close;
        SkinCursorController.UseSystemCursorFor(dialog);
        AddChild(dialog);
        _active = dialog;
        return dialog;
    }

    private void Close()
    {
        if (_active == null)
            return;
        _active.Hide();
        _active.QueueFree();
        _active = null;
    }
}
