using GodAmp.Autoload;
using Godot;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class ListOptionsButtonDropdown : ButtonDropdown
{
    [Signal] public delegate void ClearRequestedEventHandler();

    private void OnNewListButtonPressed() => EmitSignal(SignalName.ClearRequested);

    private static void OnLoadListButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LoadPlaylistRequested);
    }

    private static void OnSaveListButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.SavePlaylistRequested);
    }
}
