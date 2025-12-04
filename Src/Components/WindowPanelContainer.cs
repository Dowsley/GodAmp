using Godot;

namespace GodAmp.Components;

public partial class WindowPanelContainer : PanelContainer
{
	[Signal] public delegate void CloseButtonClickedEventHandler();
	
	[Export] public Control Contents;
	[Export] public bool MoveGlobalWindow = false;

	protected bool Minimized = false;
	protected bool Closed = false;
		
	protected bool Dragging = false;
	protected Vector2 DragOffset = Vector2.Zero;
	protected Input.MouseModeEnum PreviousMouseMode;
	
	public override void _Input(InputEvent @event)
	{
		if (Dragging && MoveGlobalWindow && @event is InputEventMouseMotion motionEvent)
		{
			float scaleFactorX = GetWindow().Size.X / GetViewport().GetVisibleRect().Size.X;
			float scaleFactorY = GetWindow().Size.Y / GetViewport().GetVisibleRect().Size.Y;
			float scaleFactor = (scaleFactorX + scaleFactorY) / 2.0f;
			
			Vector2 scaledMotion = motionEvent.Relative * scaleFactor;
			
			Vector2I windowPosition = DisplayServer.WindowGetPosition();
			windowPosition += new Vector2I((int)scaledMotion.X, (int)scaledMotion.Y);
			DisplayServer.WindowSetPosition(windowPosition);
		}
	}

	public override void _Process(double delta)
	{
		switch (Dragging)
		{
			case true when !Input.IsMouseButtonPressed(MouseButton.Left):
				Dragging = false;
				return;
			case true when !MoveGlobalWindow:
				GlobalPosition = GetGlobalMousePosition() - DragOffset;
				break;
		}
	}
	
	public virtual void OnCloseButtonPressed()
	{
		Closed = !Closed;
		Visible = !Visible;
		EmitSignal(SignalName.CloseButtonClicked);
	}

	public virtual void OnMinimizeButtonPressed()
	{
		Minimized = !Minimized;
		Contents.Visible = !Contents.Visible;
	}

    private void OnDraggablePanelInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseEvent)
        {
	        if (mouseEvent.Pressed)
	        {
		        Dragging = true;
		        if (!MoveGlobalWindow)
		        {
			        DragOffset = GetGlobalMousePosition() - GlobalPosition;
		        }
	        }
	        else
	        {
		        Dragging = false;
	        }
        }
    }
}