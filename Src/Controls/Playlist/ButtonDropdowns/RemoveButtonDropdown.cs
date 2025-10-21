using GodAmp.Autoload;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class RemoveButtonDropdown : ButtonDropdown
{
    private void OnRemoveSelectionButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.RemoveSelectedTracksFromPlaylistRequested);
    }

    // Removes everything that is not selected
    private void OnCropButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.CropPlaylistRequested);
    }

    private void OnRemoveAllButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.RemoveAllTracksFromPlaylistRequested);
    }

    private void OnRemoveMiscButtonPressed()
    {
        // TODO Implement
    }
}