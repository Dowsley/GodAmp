using Godot;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class ListOptionsButtonDropdown : ButtonDropdown
{
    [Signal] public delegate void ClearRequestedEventHandler();
    [Signal] public delegate void LoadRequestedEventHandler();
    [Signal] public delegate void SaveRequestedEventHandler();

    private void OnNewListButtonPressed() => EmitSignal(SignalName.ClearRequested);

    private void OnLoadListButtonPressed() => EmitSignal(SignalName.LoadRequested);

    private void OnSaveListButtonPressed() => EmitSignal(SignalName.SaveRequested);
}
