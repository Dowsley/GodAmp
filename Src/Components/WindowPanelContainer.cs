using System.Linq;
using GodAmp.Autoload;
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
    /// <summary>Requests a mode change from the native geometry owner.</summary>
    [Signal] public delegate void WindowshadeRequestedEventHandler(WindowPanelContainer panel, bool shaded);
    /// <summary>Notifies scene components after the presentation changes.</summary>
    [Signal] public delegate void WindowshadeChangedEventHandler(bool shaded);

    /// <summary>Expanded scene layers hidden while the compact presentation is active.</summary>
    [Export] public Godot.Collections.Array<Control> ExpandedControls { get; set; } = [];
    /// <summary>Scene-authored compact presentation, or null for windows without this mode.</summary>
    [Export] public Control? Windowshade { get; set; }
    /// <summary>Compact titlebar hit target, excluding its interactive controls.</summary>
    [Export] public Control? WindowshadeDragHitbox { get; set; }
    /// <summary>Buttons whose availability follows the current skin's compact artwork.</summary>
    [Export] public Godot.Collections.Array<BaseButton> WindowshadeButtons { get; set; } = [];
    /// <summary>Compact active titlebar atlas segments.</summary>
    [Export] public Godot.Collections.Array<AtlasTexture> WindowshadeTitlebarTextures { get; set; } = [];
    /// <summary>Offset between active and inactive compact titlebar rows.</summary>
    [Export] public int WindowshadeInactiveOffset { get; set; } = 15;
    /// <summary>Whether the current skin and scene provide a compact presentation.</summary>
    public virtual bool SupportsWindowshade => Windowshade != null;
    /// <summary>Whether the compact presentation is active.</summary>
    public bool IsWindowShaded { get; private set; }
    /// <summary>Scene-authored minimum dimensions of the expanded presentation.</summary>
    public Vector2 ExpandedMinimumSize { get; private set; }
    /// <summary>Scene-authored compact height in skin pixels.</summary>
    public int WindowshadeHeight => (int)(Windowshade?.CustomMinimumSize.Y ?? 0);

    [ExportGroup("References")]
    [Export] private Control _draggableHitbox = null!;
    /// <summary>Shared atlas segments composing the active titlebar.</summary>
    [Export] public Godot.Collections.Array<AtlasTexture> TitlebarTextures { get; set; } = [];
    /// <summary>Vertical sheet offset from active to inactive titlebar artwork.</summary>
    [Export] public int InactiveTitlebarOffset { get; set; } = 15;
    /// <summary>Resize increments in logical skin pixels, applied from the scene's minimum size.</summary>
    [Export] public Vector2I ResizeStep { get; set; } = Vector2I.One;

    private Rect2[] _activeTitlebarRegions = [];
    private Rect2[] _activeWindowshadeRegions = [];

    /// <summary>Native window hosting this panel.</summary>
    public Window WindowRef = null!;
    /// <summary>Whether a titlebar drag is in progress.</summary>
    public bool IsDragging { get; private set; }

    private Vector2I _dragOffset;
    private bool _nativeDrag;
    private bool _previousInputAccumulation;
    private Control[] _dragExclusions = [];

    /// <inheritdoc />
    public override void _Ready()
    {
        WindowRef = GetWindow();
        ExpandedMinimumSize = CustomMinimumSize;
        _dragExclusions = [.. FindChildren("*", "Control", true, false).OfType<Control>()
            .Where(control => control is BaseButton or Godot.Range || control.MouseFilter == MouseFilterEnum.Stop)];
        _activeWindowshadeRegions = [.. WindowshadeTitlebarTextures.Select(texture => texture.Region)];
        _activeTitlebarRegions = [.. TitlebarTextures.Select(texture => texture.Region)];
        WindowRef.FocusEntered += OnWindowActivated;
        WindowRef.FocusExited += OnWindowDeactivated;
        SetTitlebarActive(WindowRef.HasFocus());
        SignalBus.Instance.SkinChanged += RefreshWindowshadeAvailability;
        RefreshWindowshadeAvailability();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        FinishDrag();
        SignalBus.Instance.SkinChanged -= RefreshWindowshadeAvailability;
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
        ApplyTitlebarState(TitlebarTextures, _activeTitlebarRegions, InactiveTitlebarOffset, active);
        ApplyTitlebarState(WindowshadeTitlebarTextures, _activeWindowshadeRegions, WindowshadeInactiveOffset, active);
    }

    /// <summary>Selects atlas rows for one scene-defined titlebar presentation.</summary>
    /// <param name="textures">Mutable atlas segments referencing shared skin sheets.</param>
    /// <param name="regions">Active regions captured when the scene enters the tree.</param>
    /// <param name="inactiveOffset">Vertical displacement to the inactive artwork.</param>
    /// <param name="active">Whether the hosting window has focus.</param>
    private static void ApplyTitlebarState(Godot.Collections.Array<AtlasTexture> textures, Rect2[] regions,
        int inactiveOffset, bool active)
    {
        for (int i = 0; i < regions.Length; i++)
        {
            Rect2 region = regions[i];
            if (!active)
                region.Position += new Vector2(0, inactiveOffset);
            textures[i].Region = region;
        }
    }

    /// <summary>Updates mode buttons and requests expansion if the active skin lacks compact artwork.</summary>
    private void RefreshWindowshadeAvailability()
    {
        foreach (BaseButton button in WindowshadeButtons)
            button.Disabled = !SupportsWindowshade;
        if (IsWindowShaded && !SupportsWindowshade)
            EmitSignal(SignalName.WindowshadeRequested, this, false);
    }

    /// <summary>Selects scene layers and minimum bounds before the manager applies native geometry.</summary>
    /// <param name="shaded">True for the compact presentation, false for expanded controls.</param>
    public void SetWindowshadePresentation(bool shaded)
    {
        IsWindowShaded = shaded && SupportsWindowshade;
        FinishDrag();
        foreach (Control control in ExpandedControls)
            control.Visible = !IsWindowShaded;
        if (Windowshade != null)
            Windowshade.Visible = IsWindowShaded;
        CustomMinimumSize = IsWindowShaded
            ? new Vector2(ExpandedMinimumSize.X, WindowshadeHeight) : ExpandedMinimumSize;
        EmitSignal(SignalName.WindowshadeChanged, IsWindowShaded);
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
            IsOverInteractiveControl(button.Position))
            return;
        Control hitbox = IsWindowShaded ? WindowshadeDragHitbox! : _draggableHitbox;
        Vector2 localPosition = hitbox.GetGlobalTransformWithCanvas().AffineInverse() * button.Position;
        if (!new Rect2(Vector2.Zero, hitbox.Size).HasPoint(localPosition))
            return;
        if (button.DoubleClick && SupportsWindowshade)
        {
            OnMinimizeButtonPressed();
            GetViewport().SetInputAsHandled();
            return;
        }

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

    /// <summary>Excludes interactive controls before Godot updates GUI hover state for the press.</summary>
    /// <param name="position">Pointer position in the hosting viewport.</param>
    /// <returns>Whether a visible interactive control occupies the pressed point.</returns>
    private bool IsOverInteractiveControl(Vector2 position)
    {
        foreach (Control control in _dragExclusions)
        {
            if (!control.IsVisibleInTree())
                continue;
            Vector2 local = control.GetGlobalTransformWithCanvas().AffineInverse() * position;
            if (new Rect2(Vector2.Zero, control.Size).HasPoint(local))
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

    /// <summary>Requests the opposite presentation while preserving the existing window and controllers.</summary>
    public virtual void OnMinimizeButtonPressed()
    {
        if (SupportsWindowshade)
            EmitSignal(SignalName.WindowshadeRequested, this, !IsWindowShaded);
    }
}
