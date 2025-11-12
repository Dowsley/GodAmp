using GodAmp.Autoload;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

// TODO Implement rest of the buttons
public partial class MiscButtonDropdown : ButtonDropdown
{
    private static void OnMiscOptsButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.SelectThemeRequested);
    }
}