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
			var window = GetWindow();
			var viewportSize = GetViewport().GetVisibleRect().Size;
			float scaleFactorX = window.Size.X / viewportSize.X;
			float scaleFactorY = window.Size.Y / viewportSize.Y;
			float scaleFactor = (scaleFactorX + scaleFactorY) / 2.0f;
			
			Vector2 scaledMotion = motionEvent.Relative * scaleFactor;
			window.Position += new Vector2I((int)scaledMotion.X, (int)scaledMotion.Y);
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