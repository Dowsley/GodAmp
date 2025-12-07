using Godot;

namespace GodAmp.Components;

public partial class WindowPanelContainer : PanelContainer
{
	[Signal] public delegate void CloseButtonClickedEventHandler();
	
	[ExportGroup("References")]
	[Export] private Control _contents;
	[Export] private Control _draggableHitbox;

	public Window WindowRef;
	public bool IsDragging { get; private set; }

	protected bool Minimized = false;
	protected bool Closed = false;

	private bool _wasMousePressed;

	public override void _Ready()
	{
		WindowRef = GetWindow();
	}

	public override void _Process(double delta)
	{
		bool mousePressed = (DisplayServer.MouseGetButtonState() & MouseButtonMask.Left) != 0;
		if (!mousePressed)
		{
			IsDragging = false;
			_wasMousePressed = false;
			return;
		}
		
		var mousePos = _draggableHitbox.GetGlobalMousePosition();
		if (!_wasMousePressed && _draggableHitbox.GetGlobalRect().HasPoint(mousePos))
		{
			IsDragging = true;
			WindowRef.StartDrag();
		}
		
		_wasMousePressed = true;
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
		_contents.Visible = !_contents.Visible;
	}
}
