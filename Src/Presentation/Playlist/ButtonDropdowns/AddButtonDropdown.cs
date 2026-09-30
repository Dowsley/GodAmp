using Godot;

namespace GodAmp.Presentation.Playlist.ButtonDropdowns;

public partial class AddButtonDropdown : ButtonDropdown
{
    [Signal] public delegate void FilesRequestedEventHandler();
    [Signal] public delegate void FolderRequestedEventHandler();
    private static void OnAddUrlButtonPressed()
    {
        /* URL playback is outside the supported local-file workflow. */
    }

    private void OnAddDirButtonPressed() => EmitSignal(SignalName.FolderRequested);

    private void OnAddFileButtonPressed() => EmitSignal(SignalName.FilesRequested);
}
