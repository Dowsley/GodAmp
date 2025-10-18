using Godot;

namespace GodAmp.Components;

public partial class WindowPanelContainer : PanelContainer
{
	[Signal] public delegate void CloseButtonClickedEventHandler();
	
	[Export] public Control Contents;
	[Export] public bool MoveGlobalWindow = false;

	private bool _minimized = false;
	private bool _closed = false;
		
	private bool _dragging = false;
	private Vector2 _dragOffset = Vector2.Zero;
	private Input.MouseModeEnum _previousMouseMode;
	
	public override void _Input(InputEvent @event)
	{
		if (_dragging && MoveGlobalWindow && @event is InputEventMouseMotion motionEvent)
		{
			// Calculate the window scale factor by comparing current size to content scale
			float scaleFactorX = GetWindow().Size.X / GetViewport().GetVisibleRect().Size.X;
			float scaleFactorY = GetWindow().Size.Y / GetViewport().GetVisibleRect().Size.Y;
			float scaleFactor = (scaleFactorX + scaleFactorY) / 2.0f;
			
			Vector2 scaledMotion = motionEvent.Relative * scaleFactor;
			
			// Apply the scaled movement to the window position
			Vector2I windowPosition = DisplayServer.WindowGetPosition();
			windowPosition += new Vector2I((int)scaledMotion.X, (int)scaledMotion.Y);
			DisplayServer.WindowSetPosition(windowPosition);
		}
	}

	public override void _Process(double delta)
	{
		if (_dragging && !MoveGlobalWindow)
		{
			GlobalPosition = GetGlobalMousePosition() - _dragOffset;
		}
	}
	
	public virtual void OnCloseButtonPressed()
	{
		_closed = !_closed;
		Visible = !Visible;
		EmitSignal(SignalName.CloseButtonClicked);
	}

	public virtual void OnMinimizeButtonPressed()
	{
		_minimized = !_minimized;
		Contents.Visible = !Contents.Visible;
	}

    private void OnDraggablePanelInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                if (mouseEvent.Pressed)
                {
                    _dragging = true;
                    if (!MoveGlobalWindow)
                    {
                        _dragOffset = GetGlobalMousePosition() - GlobalPosition;
                    }
                }
                else
                {
                    _dragging = false;
                }
            }
        }
    }
}