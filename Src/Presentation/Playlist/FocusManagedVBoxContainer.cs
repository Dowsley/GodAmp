using Godot;

namespace GodAmp.Presentation.Playlist;

/// <summary>Requests dismissal when a pointer press falls outside the dropdown.</summary>
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
