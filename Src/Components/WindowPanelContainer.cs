using Godot;

namespace GodAmp.Components;

/// <summary>Provides dragging and activation artwork for a skinned native window.</summary>
public partial class WindowPanelContainer : PanelContainer
{
    /// <summary>Requests that the owning application close or hide this panel.</summary>
    [Signal] public delegate void CloseButtonClickedEventHandler();
    /// <summary>Notifies the docking manager that a panel drag has started.</summary>
    [Signal] public delegate void DragStartedEventHandler(WindowPanelContainer c);
    /// <summary>Notifies the docking manager that a panel drag has ended.</summary>
    [Signal] public delegate void DragEndedEventHandler(WindowPanelContainer c);

    [ExportGroup("References")]
    [Export] private Control _draggableHitbox = null!;
    /// <summary>Shared atlas segments composing the active titlebar.</summary>
    [Export] public Godot.Collections.Array<AtlasTexture> TitlebarTextures { get; set; } = [];
    /// <summary>Vertical sheet offset from active to inactive titlebar artwork.</summary>
    [Export] public int InactiveTitlebarOffset { get; set; } = 15;

    private Rect2[] _activeTitlebarRegions = [];

    /// <summary>Native window hosting this panel.</summary>
    public Window WindowRef = null!;
    /// <summary>Whether a titlebar drag is in progress.</summary>
    public bool IsDragging { get; private set; }

    private bool _wasMousePressed;
    private Vector2I _dragOffset;

    /// <inheritdoc />
    public override void _Ready()
    {
        WindowRef = GetWindow();
        _activeTitlebarRegions = new Rect2[TitlebarTextures.Count];
        for (int i = 0; i < TitlebarTextures.Count; i++)
            _activeTitlebarRegions[i] = TitlebarTextures[i].Region;
        WindowRef.FocusEntered += OnWindowActivated;
        WindowRef.FocusExited += OnWindowDeactivated;
        SetTitlebarActive(WindowRef.HasFocus());
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        WindowRef.FocusEntered -= OnWindowActivated;
        WindowRef.FocusExited -= OnWindowDeactivated;
        SetTitlebarActive(true);
    }

    /// <summary>Selects active artwork when this window receives focus.</summary>
    private void OnWindowActivated() => SetTitlebarActive(true);

    /// <summary>Selects inactive artwork when this window loses focus.</summary>
    private void OnWindowDeactivated() => SetTitlebarActive(false);

    /// <summary>Updates the titlebar segments while preserving their backing skin sheets.</summary>
    /// <param name="active">Whether the native window has input focus.</param>
    private void SetTitlebarActive(bool active)
    {
        for (int i = 0; i < _activeTitlebarRegions.Length; i++)
        {
            Rect2 region = _activeTitlebarRegions[i];
            if (!active)
                region.Position += new Vector2(0, InactiveTitlebarOffset);
            TitlebarTextures[i].Region = region;
        }
    }

    /// <inheritdoc />
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

        if (!_wasMousePressed && WindowRef.HasFocus() &&
            GetViewport().GuiGetHoveredControl() is not BaseButton &&
            _draggableHitbox.GetGlobalRect().HasPoint(mousePos))
        {
            IsDragging = true;
            var globalMousePos = DisplayServer.MouseGetPosition();
            _dragOffset = WindowRef.Position - globalMousePos;
            EmitSignal(SignalName.DragStarted, this);
        }

        _wasMousePressed = true;
    }

    /// <summary>Calculates the window origin that preserves the pointer's initial drag offset.</summary>
    /// <returns>Desired desktop position in screen pixels.</returns>
    public Vector2I GetDesiredPosition()
    {
        return DisplayServer.MouseGetPosition() + _dragOffset;
    }

    /// <summary>Forwards a close request to the owning application.</summary>
    public virtual void OnCloseButtonPressed()
    {
        EmitSignal(SignalName.CloseButtonClicked);
    }

    /// <summary>Reserved callback for the unsupported windowshade mode.</summary>
    public virtual void OnMinimizeButtonPressed()
    {
    }
}
