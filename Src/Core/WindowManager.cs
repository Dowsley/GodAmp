using System;
using System.Collections.Generic;
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
    
    public override void _Ready()
    {
        _equalizerWindow = _equalizer.GetParent<Window>();
        _playlistWindow = _playlist.GetParent<Window>();
        _visualizerWindow = _visualizer.GetParent<Window>();
        _masterPanelWindow = _masterPanel.GetWindow();
        
        // CenterWindow();
        
        _allContainerRefs = [ _masterPanel, _equalizer, _playlist, _visualizer ];
        _allWindowsRefs = [_masterPanelWindow, _equalizerWindow, _playlistWindow, _visualizerWindow];
        
        int width = (int)ProjectSettings.GetSetting("display/window/size/viewport_width");
        int height = (int)ProjectSettings.GetSetting("display/window/size/viewport_height");
        _originalWindowSize = new Vector2I(width, height);
        _originalVisualizerWindowSize = _visualizerWindow.Size;
        
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
        if (_windowContainerBeingDragged is { IsDragging: true })
        {
            var desiredPos = _windowContainerBeingDragged.GetDesiredPosition();
            var snappedPos = GetSnappedDragPosition(_windowContainerBeingDragged.WindowRef, desiredPos);
            _windowContainerBeingDragged.WindowRef.Position = snappedPos;
        }
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

        return new Vector2I(bestSnapX ?? desiredPos.X, bestSnapY ?? desiredPos.Y);
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
    }

    private void OnWindowDragEnd(WindowPanelContainer draggedContainerRef)
    {
        _windowContainerBeingDragged = null;
    }
    
    // private void CenterWindow()
    // {
    //     var screenSize = DisplayServer.ScreenGetSize();
    //     var windowSize = GetWindow().Size;
    //     var centeredPosition = (screenSize - windowSize) / 2;
    //     DisplayServer.WindowSetPosition(centeredPosition);
    // }
}