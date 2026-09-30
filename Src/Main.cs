using GodAmp.Settings;
using GodAmp.Presentation.Playlist;
using GodAmp.Desktop;
using GodAmp.Media.Importing;
using Godot;

namespace GodAmp;

/// <summary>Coordinates application lifetime and forwards engine-root input to feature owners.</summary>
public partial class Main : HBoxContainer
{
    [ExportGroup("Config")]
    [Export] public string DefaultSongsPath = null!;

    [ExportGroup("References")]
    [Export] private Playlist _playlist = null!;
    [Export] private AudioImportController _audioImportController = null!;
    [Export] private WindowManager _windowManager = null!;

    /// <summary>Starts restoration after child owners and views have initialized.</summary>
    public override void _Ready()
    {
        /* The engine creates the root window outside Main.tscn. */
        GetWindow().FilesDropped += OnFilesDropped;
        _audioImportController.Restore(SettingsManager.Instance.GetLastPlaylistPath(), DefaultSongsPath);
    }

    /// <summary>Disconnects root events and saves the final desktop and preference state.</summary>
    public override void _ExitTree()
    {
        GetWindow().FilesDropped -= OnFilesDropped;
        _windowManager.SaveWindowStates();
        SettingsManager.Instance.SaveAllSettings();
    }

    /// <summary>Appends a mixed OS drop without interrupting the current track.</summary>
    /// <param name="paths">Files, playlists and directories provided by the native window.</param>
    private void OnFilesDropped(string[] paths) =>
        _audioImportController.ImportDrop(paths);

    /// <inheritdoc />
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (_playlist.HandleJumpShortcut(@event))
        {
            GetViewport().SetInputAsHandled();
        }
    }
}
