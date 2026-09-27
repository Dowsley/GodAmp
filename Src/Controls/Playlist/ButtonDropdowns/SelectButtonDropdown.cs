using Godot;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class SelectButtonDropdown : ButtonDropdown
{
    [Signal] public delegate void InverseSelectionRequestedEventHandler();
    [Signal] public delegate void SelectZeroRequestedEventHandler();
    [Signal] public delegate void SelectAllRequestedEventHandler();

    private void OnInverseSelectionButtonPressed() => EmitSignal(SignalName.InverseSelectionRequested);

    private void OnSelectZeroButtonPressed() => EmitSignal(SignalName.SelectZeroRequested);

    private void OnSelectAllButtonPressed() => EmitSignal(SignalName.SelectAllRequested);
}
