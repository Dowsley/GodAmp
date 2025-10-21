using Godot;

namespace GodAmp.Controls.Playlist;

public partial class FocusManagedVBoxContainer : VBoxContainer
{
    [Signal] public delegate void FocusReleasedEventHandler();
    
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouseEvent)
            return;
    
        if (!GetGlobalRect().HasPoint(mouseEvent.GlobalPosition))
        {
            ReleaseFocus();
            EmitSignal(SignalName.FocusReleased);
        }
    }
}