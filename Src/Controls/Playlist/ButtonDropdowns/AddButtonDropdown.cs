using GodAmp.Autoload;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class AddButtonDropdown : ButtonDropdown
{
    private static void OnAddUrlButtonPressed()
    {
        // TODO Implement OnAddUrlButtonPressed
    }

    private static void OnAddDirButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LoadTracksFromDirRequested, false);
    }

    private static void OnAddFileButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LoadTracksRequested, false);
    }
}