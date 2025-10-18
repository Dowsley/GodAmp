using Godot;

namespace GodAmp.Components;

public partial class DraggablePanel : Panel
{
	[Signal] public delegate void OnDraggablePanelInputEventHandler(); 
	
	public override void _Input(InputEvent @event)
	{
		if (!Visible)
			return;
		
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.Pressed && GetGlobalRect().HasPoint(mouseEvent.GlobalPosition))
			{
				EmitSignal(SignalName.OnDraggablePanelInput, @event);
			}
			else if (!mouseEvent.Pressed)
			{
				EmitSignal(SignalName.OnDraggablePanelInput, @event);
			}
		}
	}
}