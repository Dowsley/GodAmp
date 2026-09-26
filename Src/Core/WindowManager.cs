using System;
using System.Collections.Generic;
using System.Linq;
using GodAmp.Autoload;
using GodAmp.Components;
using GodAmp.Data;
using GodAmp.Utils;
using GodAmp.Controls.Equalizer;
using GodAmp.Controls.MasterPanel;
using GodAmp.Controls.Playlist;
using Godot;

namespace GodAmp.Core;

public partial class WindowManager : Node
{
    private const int DockingDistance = 10;
    private const int GlueDistance = 5;

    [ExportGroup("References")]
    [Export] private MasterPanel _masterPanel = null!;
    [Export] private Equalizer _equalizer = null!;
    [Export] private Playlist _playlist = null!;
    [Export] private Visualizer.Visualizer _visualizer = null!;

    private WindowPanelContainer? _windowContainerBeingDragged;

    private Window _equalizerWindow = null!;
    private Window _playlistWindow = null!;
    private Window _visualizerWindow = null!;
    private Window _masterPanelWindow = null!;

    private readonly Dictionary<WindowPanelContainer, Vector2I> _logicalSizes = [];
    private readonly Dictionary<Window, (Vector2 Logical, Vector2I Applied)> _zoomOffsets = [];

    private List<WindowPanelContainer> _allContainerRefs = [];
    private List<Window> _allWindowsRefs = [];

    private readonly Dictionary<Window, HashSet<Window>> _gluedWindows = [];
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

        _allContainerRefs = [_masterPanel, _equalizer, _playlist, _visualizer];
        _allWindowsRefs = [_masterPanelWindow, _equalizerWindow, _playlistWindow, _visualizerWindow];

        foreach (WindowPanelContainer panel in _allContainerRefs)
            _logicalSizes.Add(panel, panel.WindowRef.ContentScaleSize);

        foreach (var window in _allWindowsRefs)
        {
            _gluedWindows[window] = [];
        }

        RestoreWindowStates();
    }

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
    private static Vector2I NormalizeLogicalSize(WindowPanelContainer panel, Vector2I size)
    {
        Vector2I minimum = (Vector2I)panel.ExpandedMinimumSize.Ceil();
        Vector2I step = new(Math.Max(1, panel.ResizeStep.X), Math.Max(1, panel.ResizeStep.Y));
        Vector2I extra = new(Math.Max(0, size.X - minimum.X), Math.Max(0, size.Y - minimum.Y));
        return minimum + new Vector2I(extra.X / step.X * step.X, extra.Y / step.Y * step.Y);
    }

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
        foreach (Window window in _allWindowsRefs)
        {
            if (window == _masterPanelWindow)
                continue;
            Vector2I relative = window.Position - origin;
            if (!_zoomOffsets.TryGetValue(window, out var offset) || offset.Applied != relative)
                offset = ((Vector2)relative / previousZoom, relative);
            _zoomOffsets[window] = offset;
        }
    }

    /// <summary>Restores the group origin and scales retained offsets without accumulating rounding drift.</summary>
    /// <param name="origin">Main window's desktop origin before resizing.</param>
    /// <param name="zoom">Requested zoom for the group.</param>
    private void ScaleWindowPositions(Vector2I origin, int zoom)
    {
        _masterPanelWindow.Position = origin;
        foreach (Window window in _allWindowsRefs)
        {
            if (window == _masterPanelWindow)
                continue;
            var (logical, _) = _zoomOffsets[window];
            Vector2I scaled = (Vector2I)(logical * zoom).Round();
            window.Position = origin + scaled;
            _zoomOffsets[window] = (logical, scaled);
        }
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
        int snapThreshold = DockingDistance * SettingsManager.Instance.GetZoomMode();
        var draggedSize = draggedWindow.Size;

        int? bestSnapX = null;
        int? bestSnapY = null;
        int minDistX = int.MaxValue;
        int minDistY = int.MaxValue;

        foreach (WindowPanelContainer container in _allContainerRefs)
        {
            var otherWindow = container.WindowRef;
            if (otherWindow == draggedWindow)
                continue;

            if (!otherWindow.Visible || _dragOrigins.ContainsKey(otherWindow))
                continue;

            var otherPos = otherWindow.Position;
            var otherSize = otherWindow.Size;

            bool yOverlap = !(desiredPos.Y + draggedSize.Y < otherPos.Y || desiredPos.Y > otherPos.Y + otherSize.Y);
            bool xOverlap = !(desiredPos.X + draggedSize.X < otherPos.X || desiredPos.X > otherPos.X + otherSize.X);

            if (yOverlap)
            {
                int distRightToLeft = otherPos.X - (desiredPos.X + draggedSize.X);
                if (Math.Abs(distRightToLeft) < snapThreshold && Math.Abs(distRightToLeft) < minDistX)
                {
                    minDistX = Math.Abs(distRightToLeft);
                    bestSnapX = otherPos.X - draggedSize.X;
                }

                int distLeftToRight = (otherPos.X + otherSize.X) - desiredPos.X;
                if (Math.Abs(distLeftToRight) < snapThreshold && Math.Abs(distLeftToRight) < minDistX)
                {
                    minDistX = Math.Abs(distLeftToRight);
                    bestSnapX = otherPos.X + otherSize.X;
                }
            }

            if (xOverlap)
            {
                int distBottomToTop = otherPos.Y - (desiredPos.Y + draggedSize.Y);
                if (Math.Abs(distBottomToTop) < snapThreshold && Math.Abs(distBottomToTop) < minDistY)
                {
                    minDistY = Math.Abs(distBottomToTop);
                    bestSnapY = otherPos.Y - draggedSize.Y;
                }

                int distTopToBottom = (otherPos.Y + otherSize.Y) - desiredPos.Y;
                if (Math.Abs(distTopToBottom) < snapThreshold && Math.Abs(distTopToBottom) < minDistY)
                {
                    minDistY = Math.Abs(distTopToBottom);
                    bestSnapY = otherPos.Y + otherSize.Y;
                }
            }
        }

        var finalX = bestSnapX ?? desiredPos.X;
        var finalY = bestSnapY ?? desiredPos.Y;

        if (bestSnapX.HasValue)
        {
            finalY = GetVerticalSubSnap(draggedWindow, new Vector2I(finalX, desiredPos.Y), snapThreshold) ?? finalY;
        }

        if (bestSnapY.HasValue)
        {
            finalX = GetHorizontalSubSnap(draggedWindow, new Vector2I(desiredPos.X, finalY), snapThreshold) ?? finalX;
        }

        return new Vector2I(finalX, finalY);
    }

    private int? GetVerticalSubSnap(Window draggedWindow, Vector2I snappedPos, int threshold)
    {
        var draggedSize = draggedWindow.Size;
        int? bestY = null;
        int minDist = int.MaxValue;

        foreach (WindowPanelContainer container in _allContainerRefs)
        {
            var otherWindow = container.WindowRef;
            if (!otherWindow.Visible || _dragOrigins.ContainsKey(otherWindow))
                continue;

            var otherPos = otherWindow.Position;
            var otherSize = otherWindow.Size;

            int distTop = Math.Abs(otherPos.Y - snappedPos.Y);
            if (distTop < threshold && distTop < minDist)
            {
                minDist = distTop;
                bestY = otherPos.Y;
            }

            int distBottom = Math.Abs((otherPos.Y + otherSize.Y) - (snappedPos.Y + draggedSize.Y));
            if (distBottom < threshold && distBottom < minDist)
            {
                minDist = distBottom;
                bestY = otherPos.Y + otherSize.Y - draggedSize.Y;
            }
        }

        return bestY;
    }

    private int? GetHorizontalSubSnap(Window draggedWindow, Vector2I snappedPos, int threshold)
    {
        var draggedSize = draggedWindow.Size;
        int? bestX = null;
        int minDist = int.MaxValue;

        foreach (WindowPanelContainer container in _allContainerRefs)
        {
            var otherWindow = container.WindowRef;
            if (!otherWindow.Visible || _dragOrigins.ContainsKey(otherWindow))
                continue;

            var otherPos = otherWindow.Position;
            var otherSize = otherWindow.Size;

            int distLeft = Math.Abs(otherPos.X - snappedPos.X);
            if (distLeft < threshold && distLeft < minDist)
            {
                minDist = distLeft;
                bestX = otherPos.X;
            }

            int distRight = Math.Abs((otherPos.X + otherSize.X) - (snappedPos.X + draggedSize.X));
            if (distRight < threshold && distRight < minDist)
            {
                minDist = distRight;
                bestX = otherPos.X + otherSize.X - draggedSize.X;
            }
        }

        return bestX;
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
                /* AppKit detaches native child windows when they occupy different screens. */
                if (window == lead || (_dragTransientStates.ContainsKey(window) && window.CurrentScreen == lead.CurrentScreen))
                    continue;
                Vector2I target = origin + displacement;
                if (window.Position != target)
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
        GlueToAllTouchingWindows(draggedWindow);
    }

    private void DetachFromAllWindows(Window window)
    {
        foreach (var gluedWindow in _gluedWindows[window].ToList())
        {
            _gluedWindows[window].Remove(gluedWindow);
            _gluedWindows[gluedWindow].Remove(window);
        }
    }

    private void GlueToAllTouchingWindows(Window window)
    {
        if (!window.Visible)
            return;
        int snapThreshold = GlueDistance * SettingsManager.Instance.GetZoomMode();
        foreach (var container in _allContainerRefs)
        {
            var otherWindow = container.WindowRef;
            if (!otherWindow.Visible || otherWindow == window || _gluedWindows[window].Contains(otherWindow))
                continue;

            bool touching = AreWindowsTouching(window, otherWindow, snapThreshold);
            if (touching)
            {
                _gluedWindows[window].Add(otherWindow);
                _gluedWindows[otherWindow].Add(window);
            }
        }
    }

    private HashSet<Window> GetConnectedGroup(Window startWindow)
    {
        var group = new HashSet<Window> { startWindow };
        var visited = new HashSet<Window> { startWindow };
        var toVisit = new Queue<Window>();
        toVisit.Enqueue(startWindow);

        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();
            foreach (Window connected in _gluedWindows[current]
                         .Where(window => window.Visible)
                         .Where(visited.Add))
            {
                group.Add(connected);
                toVisit.Enqueue(connected);
            }
        }

        return group;
    }

    /// <summary>Checks direct edge contacts using the same geometry as resize propagation.</summary>
    /// <param name="w1">First native window.</param>
    /// <param name="w2">Potential docking neighbor.</param>
    /// <param name="threshold">Allowed edge gap in native pixels.</param>
    /// <returns>Whether the windows share an edge contact with positive overlap.</returns>
    private static bool AreWindowsTouching(Window w1, Window w2, int threshold) =>
        WindowDockLayout.TryGetContact(new Rect2I(w1.Position, w1.Size), new Rect2I(w2.Position, w2.Size), threshold, out _);

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
        _equalizerWindow.Visible = SettingsManager.Instance.GetWindowVisible(PlayerWindow.Equalizer, true);
        _masterPanel.ToggleEqualizerButton.ButtonPressed = _equalizerWindow.Visible;
        _masterPanel.WinampMenuButton.SetEqualizerChecked(_equalizerWindow.Visible);

        var plPos = SettingsManager.Instance.GetWindowPosition(PlayerWindow.Playlist, eqPos + new Vector2I(0, _equalizerWindow.Size.Y));
        _playlistWindow.Position = plPos;
        _playlistWindow.Visible = SettingsManager.Instance.GetWindowVisible(PlayerWindow.Playlist, true);
        _masterPanel.TogglePlaylistButton.ButtonPressed = _playlistWindow.Visible;
        _masterPanel.WinampMenuButton.SetPlaylistChecked(_playlistWindow.Visible);

        var vizPos = SettingsManager.Instance.GetWindowPosition(PlayerWindow.Visualizer, masterPos + new Vector2I(windowSize.X, 0));
        _visualizerWindow.Position = vizPos;
        _visualizerWindow.Visible = SettingsManager.Instance.GetWindowVisible(PlayerWindow.Visualizer, true);
        _masterPanel.WinampMenuButton.SetVisualizerChecked(_visualizerWindow.Visible);

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
        foreach (HashSet<Window> contacts in _gluedWindows.Values)
            contacts.Clear();
        int glueThreshold = GlueDistance * SettingsManager.Instance.GetZoomMode();
        foreach (var container1 in _allContainerRefs)
        {
            foreach (var container2 in _allContainerRefs)
            {
                if (container1 == container2 || !container1.WindowRef.Visible || !container2.WindowRef.Visible)
                    continue;

                var window1 = container1.WindowRef;
                var window2 = container2.WindowRef;

                if (_gluedWindows[window1].Contains(window2))
                    continue;

                bool touching = AreWindowsTouching(window1, window2, glueThreshold);

                if (touching)
                {
                    _gluedWindows[window1].Add(window2);
                    _gluedWindows[window2].Add(window1);
                }
            }
        }
    }
}
