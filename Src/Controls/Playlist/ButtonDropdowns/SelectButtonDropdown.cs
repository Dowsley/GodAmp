using GodAmp.Autoload;
using Godot;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

public partial class SelectButtonDropdown : ButtonDropdown
{
    private void OnInverseSelectionButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.InverseSelectionRequested);
    }
    
    private void OnSelectZeroButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.SelectZeroRequested);
    }
    
    private void OnSelectAllButtonPressed()
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.SelectAllRequested);
    }
}