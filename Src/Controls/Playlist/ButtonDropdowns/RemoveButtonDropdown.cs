using Godot;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class RemoveButtonDropdown : ButtonDropdown
{
    [Signal] public delegate void RemoveSelectionRequestedEventHandler();
    [Signal] public delegate void CropRequestedEventHandler();
    [Signal] public delegate void ClearRequestedEventHandler();

    private void OnRemoveSelectionButtonPressed() => EmitSignal(SignalName.RemoveSelectionRequested);
    private void OnCropButtonPressed() => EmitSignal(SignalName.CropRequested);
    private void OnRemoveAllButtonPressed() => EmitSignal(SignalName.ClearRequested);

    private static void OnRemoveMiscButtonPressed()
    {
        // TODO Implement
    }
}
