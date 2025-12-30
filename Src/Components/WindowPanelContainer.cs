using Godot;

namespace GodAmp.Components;

public partial class WindowPanelContainer : PanelContainer
{
    [Signal] public delegate void CloseButtonClickedEventHandler();
    [Signal] public delegate void DragStartedEventHandler(WindowPanelContainer c);
    [Signal] public delegate void DragEndedEventHandler(WindowPanelContainer c);

    [ExportGroup("References")]
    [Export] private Control _draggableHitbox;

    public Window WindowRef;
    public bool IsDragging { get; private set; }

    private bool _wasMousePressed;
    private Vector2I _dragOffset;

    public override void _Ready()
    {
        WindowRef = GetWindow();
    }

    public override void _Process(double delta)
    {
        bool mousePressed = (DisplayServer.MouseGetButtonState() & MouseButtonMask.Left) != 0;

        if (!mousePressed)
        {
            if (IsDragging)
            {
                IsDragging = false;
                EmitSignal(SignalName.DragEnded, this);
            }
            _wasMousePressed = false;
            return;
        }

        var mousePos = _draggableHitbox.GetGlobalMousePosition();

        if (!_wasMousePressed && _draggableHitbox.GetGlobalRect().HasPoint(mousePos))
        {
            IsDragging = true;
            var globalMousePos = DisplayServer.MouseGetPosition();
            _dragOffset = WindowRef.Position - globalMousePos;
            EmitSignal(SignalName.DragStarted, this);
        }

        _wasMousePressed = true;
    }

    public Vector2I GetDesiredPosition()
    {
        return DisplayServer.MouseGetPosition() + _dragOffset;
    }

    public virtual void OnCloseButtonPressed()
    {
        EmitSignal(SignalName.CloseButtonClicked);
    }

    public virtual void OnMinimizeButtonPressed()
    {
        // TODO
    }
}
