using Godot;

namespace GodAmp.Components;

/// <summary>Requests logical window sizes from a scene-authored corner hit target.</summary>
public partial class WindowResizeHandle : Control
{
    /// <summary>Panel whose window is resized by this handle.</summary>
    [Export] public WindowPanelContainer Panel { get; set; } = null!;
    /// <summary>Begins a resize interaction for the owning panel.</summary>
    [Signal] public delegate void ResizeStartedEventHandler(WindowPanelContainer panel);
    /// <summary>Requests a size in unscaled skin pixels.</summary>
    [Signal] public delegate void SizeRequestedEventHandler(WindowPanelContainer panel, Vector2I size);
    /// <summary>Completes a resize interaction for the owning panel.</summary>
    [Signal] public delegate void ResizeFinishedEventHandler(WindowPanelContainer panel);

    private Window _window = null!;
    private bool _resizing;
    private Vector2I _startPointer;
    private Vector2I _startSize;
    private Vector2 _canvasScale;

    /// <inheritdoc />
    public override void _Ready()
    {
        _window = GetWindow();
        _window.FocusExited += FinishResize;
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        FinishResize();
        _window.FocusExited -= FinishResize;
    }

    /// <summary>Starts resizing when the scene's handle receives a primary-button press.</summary>
    /// <param name="input">GUI input delivered to this hit target.</param>
    private void OnGuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            return;
        _startPointer = DisplayServer.MouseGetPosition();
        _startSize = _window.ContentScaleSize;
        _canvasScale = _window.GetFinalTransform().Scale;
        _resizing = true;
        EmitSignal(SignalName.ResizeStarted, Panel);
        AcceptEvent();
    }

    /// <inheritdoc />
    public override void _Input(InputEvent input)
    {
        if (!_resizing)
            return;
        if (input is InputEventMouseMotion)
        {
            Vector2 displacement = (Vector2)(DisplayServer.MouseGetPosition() - _startPointer) / _canvasScale;
            var requested = (Vector2I)((Vector2)_startSize + displacement).Round();
            EmitSignal(SignalName.SizeRequested, Panel, new Vector2I(Mathf.Max(1, requested.X), Mathf.Max(1, requested.Y)));
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
            FinishResize();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_resizing && (DisplayServer.MouseGetButtonState() & MouseButtonMask.Left) == 0)
            FinishResize();
    }

    /// <summary>Ends the gesture once, including releases outside the window or loss of focus.</summary>
    private void FinishResize()
    {
        if (!_resizing)
            return;
        _resizing = false;
        EmitSignal(SignalName.ResizeFinished, Panel);
    }
}
