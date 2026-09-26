using System.Collections.Generic;
using GodAmp.Autoload;
using GodAmp.Data;
using Godot;

namespace GodAmp.Components;

/// <summary>Selects cached native skin cursors from scene-defined control regions.</summary>
public partial class SkinCursorController : Node
{
    /* Godot's native cursor image limit. */
    private const int MaximumCursorDimension = 256;
    /// <summary>Cursor used outside explicitly mapped controls in this window.</summary>
    [Export] public SkinCursorRole DefaultRole { get; set; }
    /// <summary>Control mappings in ascending priority; the last matching visible rectangle wins.</summary>
    [Export] public Godot.Collections.Array<SkinCursorBinding> Bindings { get; set; } = [];

    private sealed record Target(Control Control, SkinCursorRole Role);
    private readonly List<Target> _targets = [];
    private readonly Dictionary<SkinCursor, Texture2D> _cache = [];
    private static readonly HashSet<Window> _systemCursorWindows = [];
    private static SkinCursorController? _owner;
    private Window _window = null!;
    private SkinCursor? _selectedCursor;
    private Input.CursorShape _selectedShape;
    private bool _inside;
    private bool _refreshPending;

    /// <summary>Keeps system cursors active for the lifetime of a dynamically created file dialog.</summary>
    /// <param name="dialog">A picker that is attached to the scene tree and freed when closed.</param>
    public static void UseSystemCursorFor(FileDialog dialog)
    {
        if (!_systemCursorWindows.Add(dialog))
            return;
        _owner?.Clear();
        /* Native pickers do not consistently emit focus-exit events for their parent window. */
        dialog.TreeExiting += () => _systemCursorWindows.Remove(dialog);
    }

    /// <summary>Keeps system cursors active while a dynamically resolved popup is open.</summary>
    /// <param name="popup">A popup registered once by its owner during initialization.</param>
    public static void UseSystemCursorFor(Popup popup)
    {
        /* MenuButton popups and their generated submenus are resolved at runtime. */
        popup.AboutToPopup += () =>
        {
            _systemCursorWindows.Add(popup);
            _owner?.Clear();
        };
        popup.PopupHide += () => _systemCursorWindows.Remove(popup);
        popup.TreeExiting += () => _systemCursorWindows.Remove(popup);
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        foreach (SkinCursorBinding binding in Bindings)
            foreach (NodePath path in binding.ControlPaths)
            {
                Control control = GetNode<Control>(path);
                if (binding.UseVerticalScrollBar)
                    control = ((ScrollContainer)control).GetVScrollBar();
                _targets.Add(new Target(control, binding.Role));
            }
        _window = GetWindow();
        /* Native windows and autoload lifetimes are resolved when the scene is instantiated. */
        _window.MouseEntered += OnMouseEntered;
        _window.MouseExited += OnMouseExited;
        _window.FocusExited += OnMouseExited;
        SignalBus.Instance.SkinChanged += OnSkinChanged;
        SettingsManager.Instance.ZoomModeChanged += OnZoomChanged;
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        Clear();
        _window.MouseEntered -= OnMouseEntered;
        _window.MouseExited -= OnMouseExited;
        _window.FocusExited -= OnMouseExited;
        SignalBus.Instance.SkinChanged -= OnSkinChanged;
        SettingsManager.Instance.ZoomModeChanged -= OnZoomChanged;
    }

    /// <inheritdoc />
    public override void _Input(InputEvent input)
    {
        if (input is InputEventMouse)
        {
            _inside = true;
            RequestRefresh();
        }
    }

    /// <summary>Resolves mapped hit rectangles using the same canvas transforms as their controls.</summary>
    /// <param name="position">Pointer position in the owning viewport.</param>
    /// <returns>The highest-priority visible role, or the window's default role.</returns>
    public SkinCursorRole ResolveRole(Vector2 position)
    {
        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            Target target = _targets[i];
            if (target.Control.IsVisibleInTree() && new Rect2(Vector2.Zero, target.Control.Size).HasPoint(
                    target.Control.GetGlobalTransformWithCanvas().AffineInverse() * position))
                return target.Role;
        }
        return DefaultRole;
    }

    /// <summary>Refreshes when the native pointer enters this window.</summary>
    private void OnMouseEntered()
    {
        _inside = true;
        RequestRefresh();
    }

    /// <summary>Restores the system cursor when leaving this window, including through a cutout.</summary>
    private void OnMouseExited()
    {
        _inside = false;
        Clear();
    }

    /// <summary>Discards cached artwork when the skin changes and refreshes the hovered window.</summary>
    private void OnSkinChanged()
    {
        Clear();
        _cache.Clear();
        RequestRefresh();
    }

    /// <summary>Reevaluates the hovered control after UI zoom changes its hit rectangle.</summary>
    /// <param name="multiplier">UI zoom multiplier; cursor artwork retains its original dimensions.</param>
    private void OnZoomChanged(int multiplier) => RequestRefresh();

    /// <summary>Resolves the cursor after Godot has processed GUI hover and cursor-shape changes.</summary>
    private void RequestRefresh()
    {
        if (_refreshPending || !_inside)
            return;
        _refreshPending = true;
        Callable.From(Refresh).CallDeferred();
    }

    /// <summary>Applies original-size cursor artwork and hotspots when ownership, image, or shape changes.</summary>
    private void Refresh()
    {
        _refreshPending = false;
        if (!IsInsideTree() || !_inside || _systemCursorWindows.Count > 0)
            return;
        SkinCursor? cursor = SkinLoader.Instance.GetCursor(ResolveRole(GetViewport().GetMousePosition()));
        Input.CursorShape shape = Input.GetCurrentCursorShape();
        if (_owner == this && _selectedCursor == cursor && _selectedShape == shape)
            return;
        _owner?.Clear();
        if (cursor == null || cursor.Image.GetWidth() > MaximumCursorDimension ||
            cursor.Image.GetHeight() > MaximumCursorDimension)
            return;
        if (!_cache.TryGetValue(cursor, out Texture2D? texture))
        {
            texture = ImageTexture.CreateFromImage(cursor.Image);
            _cache.Add(cursor, texture);
        }
        Input.SetCustomMouseCursor(texture, shape, cursor.Hotspot);
        _owner = this;
        _selectedCursor = cursor;
        _selectedShape = shape;
    }

    /// <summary>Restores the occupied native cursor slot without clearing another window's cursor.</summary>
    private void Clear()
    {
        if (_owner != this)
            return;
        Input.SetCustomMouseCursor(null, _selectedShape);
        _owner = null;
        _selectedCursor = null;
    }
}
