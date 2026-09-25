using System.Collections.Generic;
using GodAmp.Autoload;
using GodAmp.Data;
using Godot;

namespace GodAmp.Components;

/// <summary>Selects cached native skin cursors from scene-defined control regions.</summary>
public partial class SkinCursorController : Node
{
    /* Godot's native cursor image limit and the application's supported UI zoom range. */
    private const int MaximumCursorDimension = 256;
    private const int MaximumZoom = 4;
    /// <summary>Cursor used outside explicitly mapped controls in this window.</summary>
    [Export] public SkinCursorRole DefaultRole { get; set; }
    /// <summary>Control mappings in ascending priority; the last matching visible rectangle wins.</summary>
    [Export] public Godot.Collections.Array<SkinCursorBinding> Bindings { get; set; } = [];

    private sealed record Target(Control Control, SkinCursorRole Role);
    private sealed record ScaledCursor(Texture2D Texture, Vector2 Hotspot);
    private readonly List<Target> _targets = [];
    private readonly Dictionary<(SkinCursor Cursor, int Zoom), ScaledCursor> _cache = [];
    private static SkinCursorController? _owner;
    private Window _window = null!;
    private SkinCursor? _selectedCursor;
    private Input.CursorShape _selectedShape;
    private int _selectedZoom;
    private bool _inside;
    private bool _refreshPending;

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

    /// <summary>Refreshes the selected image and hotspot after a UI zoom change.</summary>
    /// <param name="multiplier">Requested zoom; the settings manager holds the applied value.</param>
    private void OnZoomChanged(int multiplier) => RequestRefresh();

    /// <summary>Resolves the cursor after Godot has processed GUI hover and cursor-shape changes.</summary>
    private void RequestRefresh()
    {
        if (_refreshPending || !_inside)
            return;
        _refreshPending = true;
        Callable.From(Refresh).CallDeferred();
    }

    /// <summary>Uploads cursor artwork only when its owner, image, scale, or native shape slot changes.</summary>
    private void Refresh()
    {
        _refreshPending = false;
        if (!IsInsideTree() || !_inside)
            return;
        SkinCursor? cursor = SkinLoader.Instance.GetCursor(ResolveRole(GetViewport().GetMousePosition()));
        int zoom = Mathf.Clamp(SettingsManager.Instance.GetZoomMode(), 1, MaximumZoom);
        Input.CursorShape shape = Input.GetCurrentCursorShape();
        if (_owner == this && _selectedCursor == cursor && _selectedZoom == zoom && _selectedShape == shape)
            return;
        _owner?.Clear();
        if (cursor == null || cursor.Image.GetWidth() * zoom > MaximumCursorDimension ||
            cursor.Image.GetHeight() * zoom > MaximumCursorDimension)
            return;
        if (!_cache.TryGetValue((cursor, zoom), out ScaledCursor? scaled))
        {
            using var image = (Image)cursor.Image.Duplicate();
            image.Resize(image.GetWidth() * zoom, image.GetHeight() * zoom, Image.Interpolation.Nearest);
            scaled = new ScaledCursor(ImageTexture.CreateFromImage(image), (Vector2)(cursor.Hotspot * zoom));
            _cache.Add((cursor, zoom), scaled);
        }
        Input.SetCustomMouseCursor(scaled.Texture, shape, scaled.Hotspot);
        _owner = this;
        _selectedCursor = cursor;
        _selectedZoom = zoom;
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
