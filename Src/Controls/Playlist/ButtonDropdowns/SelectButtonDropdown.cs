using GodAmp.Autoload;
using Godot;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class SelectButtonDropdown : ButtonDropdown
{
    private static void OnInverseSelectionButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.InverseSelectionRequested);
    }

    private static void OnSelectZeroButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.SelectZeroRequested);
    }

    private static void OnSelectAllButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.SelectAllRequested);
    }
}