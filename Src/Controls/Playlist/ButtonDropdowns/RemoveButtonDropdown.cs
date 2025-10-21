using GodAmp.Autoload;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class RemoveButtonDropdown : ButtonDropdown
{
    private static void OnRemoveSelectionButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.RemoveSelectedTracksFromPlaylistRequested);
    }

    // Removes everything that is not selected
    private static void OnCropButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.CropPlaylistRequested);
    }

    private static void OnRemoveAllButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.RemoveAllTracksFromPlaylistRequested);
    }

    private static void OnRemoveMiscButtonPressed()
    {
        // TODO Implement
    }
}