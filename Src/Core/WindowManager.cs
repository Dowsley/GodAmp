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
    [ExportGroup("References")]
    [Export] private MasterPanel _masterPanel;
    [Export] private Equalizer _equalizer;
    [Export] private Playlist _playlist;
    [Export] private Visualizer.Visualizer _visualizer;
    
    private WindowPanelContainer _windowContainerBeingDragged;
    private bool _grabbingFocusLock;

    private Window _equalizerWindow;
    private Window _playlistWindow;
    private Window _visualizerWindow;
    private Window _masterPanelWindow;

    private Vector2I _originalWindowSize;
    private Vector2I _originalVisualizerWindowSize;

    private List<WindowPanelContainer> _allContainerRefs;
    private List<Window> _allWindowsRefs;

    private readonly Dictionary<Window, HashSet<Window>> _gluedWindows = new();
    private Vector2I _lastDraggedWindowPosition;
    
    public override void _Ready()
    {
        _equalizerWindow = _equalizer.GetParent<Window>();
        _playlistWindow = _playlist.GetParent<Window>();
        _visualizerWindow = _visualizer.GetParent<Window>();
        _masterPanelWindow = _masterPanel.GetWindow();

        _allContainerRefs = [ _masterPanel, _equalizer, _playlist, _visualizer ];
        _allWindowsRefs = [_masterPanelWindow, _equalizerWindow, _playlistWindow, _visualizerWindow];

        int width = (int)ProjectSettings.GetSetting("display/window/size/viewport_width");
        int height = (int)ProjectSettings.GetSetting("display/window/size/viewport_height");
        _originalWindowSize = new Vector2I(width, height);
        _originalVisualizerWindowSize = _visualizerWindow.Size;

        foreach (var window in _allWindowsRefs)
        {
            _gluedWindows[window] = [];
        }

        foreach (var container in _allContainerRefs)
        {
            container.DragStarted += OnWindowDragStart;
            container.DragEnded += OnWindowDragEnd;
            container.WindowRef.FocusEntered += () => OnAnyWindowFocused(container.WindowRef);
        }
    }

    public override void _Process(double delta)
    {
        ProcessDragging();
    }
    
    public void SetZoomMode(int multiplier)
    {
        var scale = new Vector2(multiplier, multiplier);

        var newSize = _originalWindowSize * multiplier;
        GetWindow().Size = newSize;
		
        _equalizerWindow.Size = newSize;
        _equalizer.Size = _originalWindowSize;
        _equalizer.Scale = scale;
		
        _playlistWindow.Size = newSize;
        _playlist.Size = _originalWindowSize;
        _playlist.Scale = scale;
		
        _visualizerWindow.Size = _originalVisualizerWindowSize * multiplier;
        _visualizer.Size = _originalVisualizerWindowSize;
        _visualizer.Scale = scale;
		
        SettingsManager.Instance.SetZoomMode(multiplier);
        // CenterWindow();
    }
    
    private void ProcessDragging()
    {
        if (_windowContainerBeingDragged is not { IsDragging: true })
            return;

        var draggedWindow = _windowContainerBeingDragged.WindowRef;
        var desiredPos = _windowContainerBeingDragged.GetDesiredPosition();
        var snappedPos = GetSnappedDragPosition(draggedWindow, desiredPos);

        var offset = snappedPos - draggedWindow.Position;
        draggedWindow.Position = snappedPos;

        MoveGluedWindows(draggedWindow, offset);
    }

    private Vector2I GetSnappedDragPosition(Window draggedWindow, Vector2I desiredPos)
    {
        const int snapThreshold = 60;
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

            if (_gluedWindows[draggedWindow].Contains(otherWindow))
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

        const int subSnapThreshold = 20;
        if (bestSnapX.HasValue)
        {
            finalY = GetVerticalSubSnap(draggedWindow, new Vector2I(finalX, desiredPos.Y), subSnapThreshold) ?? finalY;
        }

        if (bestSnapY.HasValue)
        {
            finalX = GetHorizontalSubSnap(draggedWindow, new Vector2I(desiredPos.X, finalY), subSnapThreshold) ?? finalX;
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
            if (otherWindow == draggedWindow || _gluedWindows[draggedWindow].Contains(otherWindow))
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
            if (otherWindow == draggedWindow || _gluedWindows[draggedWindow].Contains(otherWindow))
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

    private void MoveGluedWindows(Window movedWindow, Vector2I offset)
    {
        if (offset == Vector2I.Zero)
            return;

        var visited = new HashSet<Window> { movedWindow };
        var toMove = new Queue<Window>();

        foreach (var glued in _gluedWindows[movedWindow])
            toMove.Enqueue(glued);

        while (toMove.Count > 0)
        {
            var window = toMove.Dequeue();
            if (!visited.Add(window))
                continue;

            window.Position += offset;

            foreach (Window glued in _gluedWindows[window].Where(glued => !visited.Contains(glued)))
            {
                toMove.Enqueue(glued);
            }
        }
    }

    
    private void OnAnyWindowFocused(Window focusedWindow)
    {
        if (_grabbingFocusLock)
            return;
        _grabbingFocusLock = true;
        foreach (var window in _allWindowsRefs)
            window.GrabFocus();
        focusedWindow.GrabFocus();
        _masterPanelWindow.GrabFocus(); // We need this one always in the front.
        _grabbingFocusLock = false;
    }
	
    private void OnWindowDragStart(WindowPanelContainer draggedContainerRef)
    {
        _windowContainerBeingDragged = draggedContainerRef;

        if (draggedContainerRef.WindowRef != _masterPanelWindow)
        {
            DetachFromAllWindows(draggedContainerRef.WindowRef);
        }
    }

    private void OnWindowDragEnd(WindowPanelContainer draggedContainerRef)
    {
        var draggedWindow = draggedContainerRef.WindowRef;

        _windowContainerBeingDragged = null;

        var snappedToWindow = FindClosestSnappedWindow(draggedWindow);
        if (snappedToWindow != null)
        {
            GlueWindows(draggedWindow, snappedToWindow);
        }
    }

    private void DetachFromAllWindows(Window window)
    {
        foreach (var gluedWindow in _gluedWindows[window].ToList())
        {
            _gluedWindows[window].Remove(gluedWindow);
            _gluedWindows[gluedWindow].Remove(window);
        }
    }

    private void GlueWindows(Window w1, Window w2)
    {
        _gluedWindows[w1].Add(w2);
        _gluedWindows[w2].Add(w1);
    }

    private Window FindClosestSnappedWindow(Window draggedWindow)
    {
        const int snapThreshold = 5;

        return _allContainerRefs
            .Select(container => container.WindowRef)
            .Where(otherWindow => otherWindow != draggedWindow)
            .FirstOrDefault(otherWindow => AreWindowsTouching(draggedWindow, otherWindow, snapThreshold));
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
}