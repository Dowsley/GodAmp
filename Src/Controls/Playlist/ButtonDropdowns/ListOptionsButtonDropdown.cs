using GodAmp.Autoload;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class ListOptionsButtonDropdown : ButtonDropdown
{
    private static void OnNewListButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.RemoveAllTracksFromPlaylistRequested);
    }
    
    private static void OnLoadListButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LoadPlaylistRequested);
    }
    
    private static void OnSaveListButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.SavePlaylistRequested);
    }
}