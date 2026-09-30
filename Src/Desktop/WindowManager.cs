using System;
using System.Collections.Generic;
using System.Linq;
using GodAmp.Settings;
using GodAmp.Presentation.Components;
using GodAmp.Presentation.Equalizer;
using GodAmp.Presentation.MainPanel;
using GodAmp.Presentation.Playlist;
using GodAmp.Presentation.Visualizer;
using Godot;

namespace GodAmp.Desktop;

public partial class WindowManager : Node
{
    [Signal] public delegate void VisibilityChangedEventHandler(PlayerWindow window, bool visible);
    private const int DockingDistance = 10;
    private const int GlueDistance = 5;

    [ExportGroup("References")]
    [Export] private MasterPanel _masterPanel = null!;
    [Export] private Equalizer _equalizer = null!;
    [Export] private Playlist _playlist = null!;
    [Export] private Visualizer _visualizer = null!;

    private WindowPanelContainer? _windowContainerBeingDragged;

    private Window _equalizerWindow = null!;
    private Window _playlistWindow = null!;
    private Window _visualizerWindow = null!;
    private Window _masterPanelWindow = null!;

    private readonly Dictionary<WindowPanelContainer, Vector2I> _logicalSizes = [];
    private readonly WindowZoomOffsets _zoomOffsets = new();
    private readonly Dictionary<PlayerWindow, Window> _secondaryWindows = [];

    private List<WindowPanelContainer> _allContainerRefs = [];
    private List<Window> _allWindowsRefs = [];

    private readonly WindowDockGraph _docking = new();
    private readonly Dictionary<Window, PlayerWindow> _windowIdentities = [];
    private readonly Dictionary<Window, Vector2I> _dragOrigins = [];
    private readonly Dictionary<Window, bool> _dragTransientStates = [];
    private Vector2I _dragStartPosition;
    private Vector2I _lastDragPosition;
    private bool _movingDragGroup;
    private sealed record ResizeLayout(Window[] Windows, Rect2I[] Bounds, int Anchor);
    private ResizeLayout? _resizeLayout;
    private WindowPanelContainer? _resizingPanel;

    /// <inheritdoc />
    public override void _Ready()
    {
        _equalizerWindow = _equalizer.GetParent<Window>();
        _playlistWindow = _playlist.GetParent<Window>();
        _visualizerWindow = _visualizer.GetParent<Window>();
        _masterPanelWindow = _masterPanel.GetWindow();
        _secondaryWindows.Add(PlayerWindow.Equalizer, _equalizerWindow);
        _secondaryWindows.Add(PlayerWindow.Playlist, _playlistWindow);
        _secondaryWindows.Add(PlayerWindow.Visualizer, _visualizerWindow);

        _allContainerRefs = [_masterPanel, _equalizer, _playlist, _visualizer];
        _allWindowsRefs = [_masterPanelWindow, _equalizerWindow, _playlistWindow, _visualizerWindow];

        foreach (WindowPanelContainer panel in _allContainerRefs)
            _logicalSizes.Add(panel, panel.WindowRef.ContentScaleSize);

        _windowIdentities.Add(_masterPanelWindow, PlayerWindow.MasterPanel);
        foreach (var (identity, window) in _secondaryWindows)
            _windowIdentities.Add(window, identity);

        RestoreWindowStates();
    }

    /// <summary>Applies window visibility, persists the preference and publishes the resulting state.</summary>
    /// <param name="window">Secondary player window whose visibility changes.</param>
    /// <param name="visible">Whether the native window should be shown.</param>
    public void SetVisible(PlayerWindow window, bool visible)
    {
        Window target = GetSecondaryWindow(window);
        target.Visible = visible;
        SettingsManager.Instance.SetWindowVisible(window, target.Visible);
        EmitSignal(SignalName.VisibilityChanged, (int)window, target.Visible);
    }

    /// <summary>Toggles the equalizer window.</summary>
    public void ToggleEqualizer() => SetVisible(PlayerWindow.Equalizer, !_equalizerWindow.Visible);
    /// <summary>Toggles the playlist window.</summary>
    public void TogglePlaylist() => SetVisible(PlayerWindow.Playlist, !_playlistWindow.Visible);
    /// <summary>Toggles the visualizer window.</summary>
    public void ToggleVisualizer() => SetVisible(PlayerWindow.Visualizer, !_visualizerWindow.Visible);
    /// <summary>Closes the equalizer window.</summary>
    public void HideEqualizer() => SetVisible(PlayerWindow.Equalizer, false);
    /// <summary>Closes the playlist window.</summary>
    public void HidePlaylist() => SetVisible(PlayerWindow.Playlist, false);
    /// <summary>Closes the visualizer window.</summary>
    public void HideVisualizer() => SetVisible(PlayerWindow.Visualizer, false);

    private Window GetSecondaryWindow(PlayerWindow window) => _secondaryWindows.TryGetValue(window, out Window? target)
        ? target : throw new ArgumentOutOfRangeException(nameof(window), window, "A secondary player window is required.");

    /// <summary>Applies integer canvas zoom while retaining logical sizes and precise relative positions.</summary>
    /// <param name="multiplier">Requested zoom, clamped to the supported settings range.</param>
    public void SetZoomMode(int multiplier)
    {
        int oldMultiplier = SettingsManager.Instance.GetZoomMode();
        multiplier = Mathf.Clamp(multiplier, SettingsManager.MinimumZoom, SettingsManager.MaximumZoom);
        Vector2I origin = _masterPanelWindow.Position;
        CaptureWindowOffsets(origin, oldMultiplier);
        ApplyWindowGeometry(multiplier);
        ScaleWindowPositions(origin, multiplier);
        SettingsManager.Instance.SetZoomMode(multiplier);
        DetectAndRestoreGlueRelationships();
    }

    /// <summary>Gets a panel's expanded layout size independently of display zoom.</summary>
    /// <param name="panel">A panel registered with this manager.</param>
    /// <returns>Logical dimensions in skin pixels.</returns>
    public Vector2I GetLogicalSize(WindowPanelContainer panel) => _logicalSizes[panel];

    /// <summary>Changes presentation without discarding expanded dimensions or the desktop origin.</summary>
    /// <param name="panel">Registered panel requesting a compact or expanded presentation.</param>
    /// <param name="shaded">Requested compact mode; unsupported skins stay expanded.</param>
    public void SetWindowShaded(WindowPanelContainer panel, bool shaded)
    {
        if (!_logicalSizes.TryGetValue(panel, out Vector2I expandedSize) || (shaded && !panel.SupportsWindowshade))
            return;
        if (panel.IsWindowShaded == shaded)
            return;
        ResizeLayout layout = CaptureResizeLayout(panel);
        panel.SetWindowshadePresentation(shaded);
        ApplyPanelGeometry(panel, expandedSize, SettingsManager.Instance.GetZoomMode());
        ApplyResizeLayout(layout, panel.WindowRef.Size);
        SaveWindowStates();
        SettingsManager.Instance.SaveAllSettings();
    }

    /// <summary>Updates a resizable panel's logical size without changing the global zoom.</summary>
    /// <param name="panel">The playlist or visualizer panel.</param>
    /// <param name="size">Positive dimensions in skin pixels, clamped to the scene's minimum size.</param>
    /// <exception cref="ArgumentException">The panel is fixed-size or the dimensions are not positive.</exception>
    public void SetLogicalSize(WindowPanelContainer panel, Vector2I size)
    {
        if (panel != _playlist && panel != _visualizer)
            throw new ArgumentException("Only playlist and visualizer panels support resizing.", nameof(panel));
        if (size.X <= 0 || size.Y <= 0)
            throw new ArgumentException("A positive size is required for a resizable panel.", nameof(size));
        if (panel.IsWindowShaded)
            size.Y = _logicalSizes[panel].Y;
        Vector2I normalized = NormalizeLogicalSize(panel, size);
        if (_logicalSizes[panel] == normalized)
            return;
        ResizeLayout layout = _resizingPanel == panel && _resizeLayout != null ? _resizeLayout : CaptureResizeLayout(panel);
        _logicalSizes[panel] = normalized;
        ApplyPanelGeometry(panel, _logicalSizes[panel], SettingsManager.Instance.GetZoomMode());
        ApplyResizeLayout(layout, panel.WindowRef.Size);
    }

    /// <summary>Clamps a requested layout to the scene minimum and snaps to its resize increments.</summary>
    /// <param name="panel">Panel defining minimum dimensions and increments.</param>
    /// <param name="size">Requested dimensions in logical pixels.</param>
    /// <returns>Dimensions on the scene's resize grid.</returns>
    private static Vector2I NormalizeLogicalSize(WindowPanelContainer panel, Vector2I size) =>
        WindowGeometry.NormalizeSize(size, (Vector2I)panel.ExpandedMinimumSize.Ceil(), panel.ResizeStep);

    /// <summary>Captures edge contacts once so a gesture cannot pick up new neighbors while resizing.</summary>
    /// <param name="panel">Panel whose scene-owned handle begins resizing.</param>
    private void OnWindowResizeStarted(WindowPanelContainer panel)
    {
        _resizingPanel = panel;
        _resizeLayout = CaptureResizeLayout(panel);
    }

    /// <summary>Captures visible window rectangles in deterministic application order.</summary>
    /// <param name="panel">Anchored window whose dimensions will change.</param>
    /// <returns>Native bounds and the anchored window's index; hidden windows resize independently.</returns>
    private ResizeLayout CaptureResizeLayout(WindowPanelContainer panel)
    {
        Window[] windows = panel.WindowRef.Visible
            ? [.. _allWindowsRefs.Where(window => window.Visible)] : [panel.WindowRef];
        return new ResizeLayout(windows, [.. windows.Select(window => new Rect2I(window.Position, window.Size))],
            Array.IndexOf(windows, panel.WindowRef));
    }

    /// <summary>Moves each affected neighbor once and rebuilds contacts from the resulting geometry.</summary>
    /// <param name="layout">Snapshot preceding the mode change or resize gesture.</param>
    /// <param name="size">Actual native dimensions of the resized window.</param>
    private void ApplyResizeLayout(ResizeLayout layout, Vector2I size)
    {
        Vector2I[] positions = WindowDockLayout.Resize(layout.Bounds, layout.Anchor, size,
            GlueDistance * SettingsManager.Instance.GetZoomMode());
        for (int i = 0; i < layout.Windows.Length; i++)
            if (layout.Windows[i].Position != positions[i])
                layout.Windows[i].Position = positions[i];
        DetectAndRestoreGlueRelationships();
    }

    /// <summary>Rebuilds contacts and records logical sizes when the gesture ends.</summary>
    /// <param name="panel">Panel whose resize interaction has completed.</param>
    private void OnWindowResizeFinished(WindowPanelContainer panel)
    {
        if (_resizingPanel != panel)
            return;
        _resizeLayout = null;
        _resizingPanel = null;
        DetectAndRestoreGlueRelationships();
        SaveWindowStates();
        SettingsManager.Instance.SaveAllSettings();
    }

    /// <summary>Captures offsets before native resizing can reposition windows to fit the display.</summary>
    /// <param name="origin">Main window's desktop origin before resizing.</param>
    /// <param name="previousZoom">Zoom at which current native positions were established.</param>
    private void CaptureWindowOffsets(Vector2I origin, int previousZoom)
    {
        foreach (var (identity, window) in _secondaryWindows)
            _zoomOffsets.Capture(identity, window.Position - origin, previousZoom);
    }

    /// <summary>Restores the group origin and scales retained offsets without accumulating rounding drift.</summary>
    /// <param name="origin">Main window's desktop origin before resizing.</param>
    /// <param name="zoom">Requested zoom for the group.</param>
    private void ScaleWindowPositions(Vector2I origin, int zoom)
    {
        _masterPanelWindow.Position = origin;
        foreach (var (identity, window) in _secondaryWindows)
            window.Position = origin + _zoomOffsets.Scale(identity, zoom);
    }

    /// <summary>Moves the docked group directly from native movement or fallback input notifications.</summary>
    /// <param name="panel">Panel emitting the movement notification.</param>
    /// <param name="position">Native window origin or fallback pointer-relative origin in desktop pixels.</param>
    private void OnWindowDragMoved(WindowPanelContainer panel, Vector2I position)
    {
        if (_windowContainerBeingDragged != panel || _movingDragGroup)
            return;
        MoveDragGroup(position);
    }

    private Vector2I GetSnappedDragPosition(Window draggedWindow, Vector2I desiredPos)
    {
        Rect2I[] neighbors = [.. _allWindowsRefs
            .Where(window => window != draggedWindow && window.Visible && !_dragOrigins.ContainsKey(window))
            .Select(window => new Rect2I(window.Position, window.Size))];
        return WindowSnapping.Snap(new Rect2I(desiredPos, draggedWindow.Size), neighbors,
            DockingDistance * SettingsManager.Instance.GetZoomMode());
    }

    /// <summary>Applies one absolute displacement to the cached group without accumulating rounding drift.</summary>
    /// <param name="position">Desktop origin of the dragged window.</param>
    private void MoveDragGroup(Vector2I position)
    {
        if (_movingDragGroup || position == _lastDragPosition)
            return;
        _movingDragGroup = true;
        try
        {
            Vector2I displacement = position - _dragStartPosition;
            Window lead = _windowContainerBeingDragged!.WindowRef;
            if (lead.Position != position)
                lead.Position = position;
            foreach (var (window, origin) in _dragOrigins)
            {
                if (window == lead)
                    continue;
                /* Native child movement can precede the follower's cached Window.Position update. */
                Vector2I target = origin + displacement;
                Vector2I current = _dragTransientStates.ContainsKey(window)
                    ? DisplayServer.WindowGetPosition(window.GetWindowId()) : window.Position;
                if (current != target)
                    window.Position = target;
            }
            _lastDragPosition = position;
        }
        finally
        {
            _movingDragGroup = false;
        }
    }

    /// <summary>Captures the connected group once, detaching a directly dragged secondary window.</summary>
    /// <param name="draggedContainerRef">Panel whose titlebar initiated the drag.</param>
    private void OnWindowDragStart(WindowPanelContainer draggedContainerRef)
    {
        RestoreDragTransients();
        DetectAndRestoreGlueRelationships();
        _windowContainerBeingDragged = draggedContainerRef;

        if (draggedContainerRef.WindowRef != _masterPanelWindow)
        {
            DetachFromAllWindows(draggedContainerRef.WindowRef);
        }
        _dragOrigins.Clear();
        foreach (Window window in GetConnectedGroup(draggedContainerRef.WindowRef))
        {
            if (window.Visible)
                _dragOrigins[window] = window.Position;
        }
        _dragStartPosition = draggedContainerRef.WindowRef.Position;
        _lastDragPosition = _dragStartPosition;
        if (OperatingSystem.IsMacOS() && draggedContainerRef.WindowRef == _masterPanelWindow)
        {
            foreach (Window window in _dragOrigins.Keys)
            {
                if (window == _masterPanelWindow || window.IsEmbedded() || window.AlwaysOnTop)
                    continue;
                _dragTransientStates[window] = window.Transient;
                window.Transient = true;
            }
        }
    }

    /// <summary>Restores window ownership after the native compositor finishes moving the docked group.</summary>
    private void RestoreDragTransients()
    {
        foreach (var (window, transient) in _dragTransientStates)
        {
            if (IsInstanceValid(window))
                window.Transient = transient;
        }
        _dragTransientStates.Clear();
    }

    /// <inheritdoc />
    public override void _ExitTree() => RestoreDragTransients();

    /// <summary>Snaps the released group and records its docking relationships.</summary>
    /// <param name="draggedContainerRef">Panel whose drag has ended.</param>
    private void OnWindowDragEnd(WindowPanelContainer draggedContainerRef)
    {
        if (_windowContainerBeingDragged != draggedContainerRef)
            return;
        var draggedWindow = draggedContainerRef.WindowRef;
        MoveDragGroup(GetSnappedDragPosition(draggedWindow, draggedWindow.Position));
        RestoreDragTransients();
        _windowContainerBeingDragged = null;
        _dragOrigins.Clear();
        DetectAndRestoreGlueRelationships();
    }

    private void DetachFromAllWindows(Window window) => _docking.Detach(_windowIdentities[window]);

    private IEnumerable<Window> GetConnectedGroup(Window startWindow)
    {
        IReadOnlySet<PlayerWindow> group = _docking.GetGroup(_windowIdentities[startWindow]);
        return _allWindowsRefs.Where(window => group.Contains(_windowIdentities[window]));
    }

    /// <summary>Records expanded logical sizes, compact modes, desktop positions, and visibility.</summary>
    public void SaveWindowStates()
    {
        SettingsManager.Instance.SetWindowShaded(PlayerWindow.MasterPanel, _masterPanel.IsWindowShaded);
        SettingsManager.Instance.SetWindowShaded(PlayerWindow.Equalizer, _equalizer.IsWindowShaded);
        SettingsManager.Instance.SetWindowShaded(PlayerWindow.Playlist, _playlist.IsWindowShaded);
        SettingsManager.Instance.SetWindowSize(PlayerWindow.Playlist, _logicalSizes[_playlist]);
        SettingsManager.Instance.SetWindowSize(PlayerWindow.Visualizer, _logicalSizes[_visualizer]);
        SettingsManager.Instance.SetWindowPosition(PlayerWindow.MasterPanel, _masterPanelWindow.Position);

        SettingsManager.Instance.SetWindowPosition(PlayerWindow.Equalizer, _equalizerWindow.Position);
        SettingsManager.Instance.SetWindowVisible(PlayerWindow.Equalizer, _equalizerWindow.Visible);

        SettingsManager.Instance.SetWindowPosition(PlayerWindow.Playlist, _playlistWindow.Position);
        SettingsManager.Instance.SetWindowVisible(PlayerWindow.Playlist, _playlistWindow.Visible);

        SettingsManager.Instance.SetWindowPosition(PlayerWindow.Visualizer, _visualizerWindow.Position);
        SettingsManager.Instance.SetWindowVisible(PlayerWindow.Visualizer, _visualizerWindow.Visible);
    }

    /// <summary>Restores geometry before native positions, visibility, and docking relationships.</summary>
    private void RestoreWindowStates()
    {
        int zoomMultiplier = SettingsManager.Instance.GetZoomMode();

        _logicalSizes[_playlist] = NormalizeLogicalSize(_playlist,
            SettingsManager.Instance.GetWindowSize(PlayerWindow.Playlist, _logicalSizes[_playlist]));
        _logicalSizes[_visualizer] = NormalizeLogicalSize(_visualizer,
            SettingsManager.Instance.GetWindowSize(PlayerWindow.Visualizer, _logicalSizes[_visualizer]));

        _masterPanel.SetWindowshadePresentation(SettingsManager.Instance.GetWindowShaded(PlayerWindow.MasterPanel));
        _equalizer.SetWindowshadePresentation(SettingsManager.Instance.GetWindowShaded(PlayerWindow.Equalizer));
        _playlist.SetWindowshadePresentation(SettingsManager.Instance.GetWindowShaded(PlayerWindow.Playlist));

        ApplyWindowGeometry(zoomMultiplier);

        var screenSize = DisplayServer.ScreenGetSize();
        var windowSize = _masterPanelWindow.Size;
        var totalGroupSize = new Vector2I(Math.Max(windowSize.X, _playlistWindow.Size.X) + _visualizerWindow.Size.X,
            Math.Max(windowSize.Y + _equalizerWindow.Size.Y + _playlistWindow.Size.Y, _visualizerWindow.Size.Y));
        var groupCenteredPos = (screenSize - totalGroupSize) / 2;

        var masterPos = SettingsManager.Instance.GetWindowPosition(PlayerWindow.MasterPanel, groupCenteredPos);
        _masterPanelWindow.Position = masterPos;

        var eqPos = SettingsManager.Instance.GetWindowPosition(PlayerWindow.Equalizer, masterPos + new Vector2I(0, windowSize.Y));
        _equalizerWindow.Position = eqPos;
        SetVisible(PlayerWindow.Equalizer, SettingsManager.Instance.GetWindowVisible(PlayerWindow.Equalizer, true));

        var plPos = SettingsManager.Instance.GetWindowPosition(PlayerWindow.Playlist, eqPos + new Vector2I(0, _equalizerWindow.Size.Y));
        _playlistWindow.Position = plPos;
        SetVisible(PlayerWindow.Playlist, SettingsManager.Instance.GetWindowVisible(PlayerWindow.Playlist, true));

        var vizPos = SettingsManager.Instance.GetWindowPosition(PlayerWindow.Visualizer, masterPos + new Vector2I(windowSize.X, 0));
        _visualizerWindow.Position = vizPos;
        SetVisible(PlayerWindow.Visualizer, SettingsManager.Instance.GetWindowVisible(PlayerWindow.Visualizer, true));

        DetectAndRestoreGlueRelationships();
    }

    /// <summary>Applies per-window logical dimensions through Godot's scene-configured canvas scaling.</summary>
    /// <param name="multiplier">Validated integer zoom shared by all player windows.</param>
    private void ApplyWindowGeometry(int multiplier)
    {
        foreach (var (panel, logicalSize) in _logicalSizes)
            ApplyPanelGeometry(panel, logicalSize, multiplier);
    }

    /// <summary>Sets one window's virtual canvas and native dimensions without scaling its panel node.</summary>
    /// <param name="panel">Scene-authored panel in the target native window.</param>
    /// <param name="logicalSize">Layout dimensions in skin pixels.</param>
    /// <param name="multiplier">Validated integer UI zoom.</param>
    private static void ApplyPanelGeometry(WindowPanelContainer panel, Vector2I logicalSize, int multiplier)
    {
        if (panel.IsWindowShaded)
            logicalSize.Y = panel.WindowshadeHeight;
        Window window = panel.WindowRef;
        window.ContentScaleSize = logicalSize;
        window.Size = logicalSize * multiplier;
        panel.Size = logicalSize;
    }

    private void DetectAndRestoreGlueRelationships()
    {
        WindowBounds[] windows = [.. _allWindowsRefs.Select(window => new WindowBounds(
            _windowIdentities[window], new Rect2I(window.Position, window.Size), window.Visible))];
        _docking.Rebuild(windows, GlueDistance * SettingsManager.Instance.GetZoomMode());
    }
}
