using System.Linq;
using Godot;

namespace GodAmp.Components;

/// <summary>Provides dragging and activation artwork for a skinned native window.</summary>
public partial class WindowPanelContainer : PanelContainer
{
    /// <summary>Requests that the owning application close or hide this panel.</summary>
    [Signal] public delegate void CloseButtonClickedEventHandler();
    /// <summary>Notifies the docking manager that a panel drag has started.</summary>
    [Signal] public delegate void DragStartedEventHandler(WindowPanelContainer c);
    /// <summary>Notifies the docking manager immediately when the dragged window moves.</summary>
    [Signal] public delegate void DragMovedEventHandler(WindowPanelContainer c, Vector2I position);
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

    private Vector2I _dragOffset;
    private bool _nativeDrag;
    private bool _previousInputAccumulation;
    private BaseButton[] _buttons = [];

    /// <inheritdoc />
    public override void _Ready()
    {
        WindowRef = GetWindow();
        _buttons = [.. FindChildren("*", "BaseButton", true, false).OfType<BaseButton>()];
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
        FinishDrag();
        WindowRef.FocusEntered -= OnWindowActivated;
        WindowRef.FocusExited -= OnWindowDeactivated;
        SetTitlebarActive(true);
    }

    /// <summary>Selects active artwork when this window receives focus.</summary>
    private void OnWindowActivated() => SetTitlebarActive(true);

    /// <summary>Selects inactive artwork when this window loses focus.</summary>
    private void OnWindowDeactivated()
    {
        SetTitlebarActive(false);
        FinishDrag();
    }

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
    public override void _Input(InputEvent input)
    {
        if (IsDragging)
        {
            if (!_nativeDrag && input is InputEventMouseMotion)
                EmitSignal(SignalName.DragMoved, this, DisplayServer.MouseGetPosition() + _dragOffset);
            return;
        }
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button ||
            IsOverButton(button.Position))
            return;
        Vector2 localPosition = _draggableHitbox.GetGlobalTransformWithCanvas().AffineInverse() * button.Position;
        if (!new Rect2(Vector2.Zero, _draggableHitbox.Size).HasPoint(localPosition))
            return;

        WindowRef.GrabFocus();
        IsDragging = true;
        _nativeDrag = !WindowRef.IsEmbedded() && DisplayServer.HasFeature(DisplayServer.Feature.WindowDrag);
        _dragOffset = WindowRef.Position - DisplayServer.MouseGetPosition();
        _previousInputAccumulation = Input.UseAccumulatedInput;
        if (!_nativeDrag)
            Input.UseAccumulatedInput = false;
        GetViewport().SetInputAsHandled();
        EmitSignal(SignalName.DragStarted, this);
        if (_nativeDrag)
            WindowRef.StartDrag();
        CheckDragReleased();
    }

    /// <summary>Excludes button hitboxes before Godot updates its GUI hover state for the press.</summary>
    /// <param name="position">Pointer position in the hosting viewport.</param>
    /// <returns>Whether a visible button occupies the pressed point.</returns>
    private bool IsOverButton(Vector2 position)
    {
        foreach (BaseButton button in _buttons)
        {
            if (!button.IsVisibleInTree())
                continue;
            Vector2 local = button.GetGlobalTransformWithCanvas().AffineInverse() * position;
            if (new Rect2(Vector2.Zero, button.Size).HasPoint(local))
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    public override void _Notification(int what)
    {
        if (what == NotificationWMPositionChanged && IsDragging && _nativeDrag)
            EmitSignal(SignalName.DragMoved, this, WindowRef.Position);
    }

    /// <inheritdoc />
    public override void _Process(double delta) => CheckDragReleased();

    /// <summary>Checks physical release because native movement can synthesize GUI button releases.</summary>
    private void CheckDragReleased()
    {
        if (IsDragging && (DisplayServer.MouseGetButtonState() & MouseButtonMask.Left) == 0)
            FinishDrag();
    }

    /// <summary>Finishes native or fallback dragging and restores the input accumulation setting.</summary>
    private void FinishDrag()
    {
        if (!IsDragging)
            return;
        IsDragging = false;
        if (!_nativeDrag)
            Input.UseAccumulatedInput = _previousInputAccumulation;
        EmitSignal(SignalName.DragEnded, this);
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
