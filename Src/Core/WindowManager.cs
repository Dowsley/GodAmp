using System;
using System.Collections.Generic;
using System.Linq;
using GodAmp.Autoload;
using GodAmp.Components;
using GodAmp.Controls.Equalizer;
using GodAmp.Controls.MasterPanel;
using GodAmp.Controls.Playlist;
using Godot;

namespace GodAmp.Core;

public partial class WindowManager : Node
{
    private const string MasterPanelWindowName = "masterPanel";
    private const string EqualizerWindowName = "equalizer";
    private const string PlaylistWindowName = "playlist";
    private const string VisualizerWindowName = "visualizer";
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
    }

    /// <summary>Gets a panel's expanded layout size independently of display zoom.</summary>
    /// <param name="panel">A panel registered with this manager.</param>
    /// <returns>Logical dimensions in skin pixels.</returns>
    public Vector2I GetLogicalSize(WindowPanelContainer panel) => _logicalSizes[panel];

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
        Vector2 minimum = panel.GetCombinedMinimumSize().Ceil();
        _logicalSizes[panel] = new Vector2I(Math.Max(size.X, (int)minimum.X), Math.Max(size.Y, (int)minimum.Y));
        Vector2I position = panel.WindowRef.Position;
        ApplyPanelGeometry(panel, _logicalSizes[panel], SettingsManager.Instance.GetZoomMode());
        panel.WindowRef.Position = position;
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
        int snapThreshold = GlueDistance * SettingsManager.Instance.GetZoomMode();
        var touchingWindows = new List<Window>();
        foreach (var container in _allContainerRefs)
        {
            var otherWindow = container.WindowRef;
            if (!otherWindow.Visible || otherWindow == window || _gluedWindows[window].Contains(otherWindow))
                continue;

            bool touching = AreWindowsTouching(window, otherWindow, snapThreshold);
            if (touching)
                touchingWindows.Add(otherWindow);
        }

        if (touchingWindows.Count > 0)
        {
            var allWindowsToGlueTo = GetAllWindowsInGroups(touchingWindows);

            foreach (var w in allWindowsToGlueTo)
            {
                _gluedWindows[window].Add(w);
                _gluedWindows[w].Add(window);
            }
        }
    }

    private HashSet<Window> GetAllWindowsInGroups(List<Window> touchingWindows)
    {
        var result = new HashSet<Window>();

        foreach (var w in touchingWindows
                     .Select(GetConnectedGroup)
                     .SelectMany(group => group))
        {
            result.Add(w);
        }

        return result;
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

    private static bool AreWindowsTouching(Window w1, Window w2, int threshold)
    {
        var pos1 = w1.Position;
        var size1 = w1.Size;
        var pos2 = w2.Position;
        var size2 = w2.Size;

        bool yOverlap = !(pos1.Y + size1.Y < pos2.Y || pos1.Y > pos2.Y + size2.Y);
        bool xOverlap = !(pos1.X + size1.X < pos2.X || pos1.X > pos2.X + size2.X);

        if (yOverlap)
        {
            int distRightToLeft = Math.Abs(pos2.X - (pos1.X + size1.X));
            int distLeftToRight = Math.Abs((pos2.X + size2.X) - pos1.X);
            if (distRightToLeft <= threshold || distLeftToRight <= threshold)
                return true;
        }

        if (xOverlap)
        {
            int distBottomToTop = Math.Abs(pos2.Y - (pos1.Y + size1.Y));
            int distTopToBottom = Math.Abs((pos2.Y + size2.Y) - pos1.Y);
            if (distBottomToTop <= threshold || distTopToBottom <= threshold)
                return true;
        }

        return false;
    }

    public void SaveWindowStates()
    {
        SettingsManager.Instance.SetWindowPosition(MasterPanelWindowName, _masterPanelWindow.Position);

        SettingsManager.Instance.SetWindowPosition(EqualizerWindowName, _equalizerWindow.Position);
        SettingsManager.Instance.SetWindowVisible(EqualizerWindowName, _equalizerWindow.Visible);

        SettingsManager.Instance.SetWindowPosition(PlaylistWindowName, _playlistWindow.Position);
        SettingsManager.Instance.SetWindowVisible(PlaylistWindowName, _playlistWindow.Visible);

        SettingsManager.Instance.SetWindowPosition(VisualizerWindowName, _visualizerWindow.Position);
        SettingsManager.Instance.SetWindowVisible(VisualizerWindowName, _visualizerWindow.Visible);
    }

    /// <summary>Restores geometry before native positions, visibility, and docking relationships.</summary>
    private void RestoreWindowStates()
    {
        int zoomMultiplier = SettingsManager.Instance.GetZoomMode();

        ApplyWindowGeometry(zoomMultiplier);

        var screenSize = DisplayServer.ScreenGetSize();
        var windowSize = _masterPanelWindow.Size;
        var totalGroupSize = new Vector2I(windowSize.X + _visualizerWindow.Size.X, windowSize.Y * 3);
        var groupCenteredPos = (screenSize - totalGroupSize) / 2;

        var masterPos = SettingsManager.Instance.GetWindowPosition(MasterPanelWindowName, groupCenteredPos);
        _masterPanelWindow.Position = masterPos;

        var eqPos = SettingsManager.Instance.GetWindowPosition(EqualizerWindowName, groupCenteredPos + new Vector2I(0, windowSize.Y));
        _equalizerWindow.Position = eqPos;
        _equalizerWindow.Visible = SettingsManager.Instance.GetWindowVisible(EqualizerWindowName, true);
        _masterPanel.ToggleEqualizerButton.ButtonPressed = _equalizerWindow.Visible;
        _masterPanel.WinampMenuButton.SetEqualizerChecked(_equalizerWindow.Visible);

        var plPos = SettingsManager.Instance.GetWindowPosition(PlaylistWindowName, groupCenteredPos + new Vector2I(0, windowSize.Y * 2));
        _playlistWindow.Position = plPos;
        _playlistWindow.Visible = SettingsManager.Instance.GetWindowVisible(PlaylistWindowName, true);
        _masterPanel.TogglePlaylistButton.ButtonPressed = _playlistWindow.Visible;
        _masterPanel.WinampMenuButton.SetPlaylistChecked(_playlistWindow.Visible);

        var vizPos = SettingsManager.Instance.GetWindowPosition(VisualizerWindowName, groupCenteredPos + new Vector2I(windowSize.X, 0));
        _visualizerWindow.Position = vizPos;
        _visualizerWindow.Visible = SettingsManager.Instance.GetWindowVisible(VisualizerWindowName, true);
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
        Window window = panel.WindowRef;
        window.ContentScaleSize = logicalSize;
        window.Size = logicalSize * multiplier;
        panel.Size = logicalSize;
    }

    private void DetectAndRestoreGlueRelationships()
    {
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
