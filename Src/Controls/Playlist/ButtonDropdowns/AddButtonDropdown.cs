using Godot;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class AddButtonDropdown : ButtonDropdown
{
    [Signal] public delegate void FilesRequestedEventHandler();
    [Signal] public delegate void FolderRequestedEventHandler();
    private static void OnAddUrlButtonPressed()
    {
        // TODO Implement OnAddUrlButtonPressed
    }

    private void OnAddDirButtonPressed() => EmitSignal(SignalName.FolderRequested);

    private void OnAddFileButtonPressed() => EmitSignal(SignalName.FilesRequested);
}
