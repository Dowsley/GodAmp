using GodAmp.Autoload;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class AddButtonDropdown : ButtonDropdown
{
    public void OnAddUrlButtonPressed()
    {
        // TODO Implement OnAddUrlButtonPressed
    }

    public void OnAddDirButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LoadTracksFromDirRequested, false);
    }

    public void OnAddFileButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.LoadTracksRequested, false);
    }
}