using GodAmp.Autoload;
using GodAmp.Data;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Components;

/// <summary>Applies a skin contour to the final window pixels and native mouse region together.</summary>
public partial class SkinWindowShape : CanvasLayer
{
    /// <summary>The panel whose local coordinates define the skin contour.</summary>
    [Export] public Control Panel { get; set; } = null!;
    /// <summary>The classic window mode represented by this scene.</summary>
    [Export] public SkinRegionMode Mode { get; set; }
    [Export] private ColorRect _mask = null!;

    private Window _window = null!;
    private bool _refreshPending;

    /// <inheritdoc />
    public override void _Ready()
    {
        _window = GetWindow();
        /* The hosting native window and autoloads are resolved at runtime. */
        _window.SizeChanged += RequestRefresh;
        SignalBus.Instance.SkinChanged += RequestRefresh;
        SettingsManager.Instance.ZoomModeChanged += OnZoomChanged;
        RequestRefresh();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _window.SizeChanged -= RequestRefresh;
        SignalBus.Instance.SkinChanged -= RequestRefresh;
        SettingsManager.Instance.ZoomModeChanged -= OnZoomChanged;
        _window.MousePassthroughPolygon = [];
    }

    /// <summary>Refreshes after the docking manager has assigned panel scales and window sizes.</summary>
    /// <param name="multiplier">The requested UI zoom; geometry uses the resulting transforms.</param>
    private void OnZoomChanged(int multiplier) => RequestRefresh();

    /// <summary>Coalesces geometry changes until scene layout has settled.</summary>
    private void RequestRefresh()
    {
        if (_refreshPending)
            return;
        _refreshPending = true;
        Callable.From(Refresh).CallDeferred();
    }

    /// <summary>Prepares both representations before replacing the active window shape.</summary>
    private void Refresh()
    {
        _refreshPending = false;
        if (!IsInsideTree())
            return;
        var contours = SkinLoader.Instance.Regions.GetPolygons(Mode);
        string backend = DisplayServer.GetName();
        bool supported = !_window.IsEmbedded() && _window.Transparent && _window.TransparentBg &&
            DisplayServer.IsWindowTransparencyAvailable() && backend is "macOS" or "Windows" or "X11";
        Vector2[] polygon = supported
            ? SkinRegionGeometry.Prepare(contours, _window.GetFinalTransform() * Panel.GetGlobalTransformWithCanvas(), _window.Size)
            : [];
        if (polygon.Length == 0)
        {
            _mask.Hide();
            _window.MousePassthroughPolygon = [];
            if (contours.Count > 0)
                GD.PushWarning($"Using a rectangular {Mode} window: skin geometry or native shaping is unsupported ({backend}).");
            return;
        }

        using Image image = SkinRegionGeometry.CreateMask(polygon, _window.Size);
        var texture = ImageTexture.CreateFromImage(image);
        ((ShaderMaterial)_mask.Material).SetShaderParameter("region_mask", texture);
        _window.MousePassthroughPolygon = polygon;
        _mask.Show();
    }
}
