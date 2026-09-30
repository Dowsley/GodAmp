using System;
using System.Collections.Generic;
using Godot;

namespace GodAmp.Desktop;

/// <summary>Retains precise logical offsets between zoom changes without retaining native windows.</summary>
internal sealed class WindowZoomOffsets
{
    private readonly Dictionary<PlayerWindow, (Vector2 Logical, Vector2I Applied)> _offsets = [];

    /// <summary>Captures movement while preserving precision when the applied offset is unchanged.</summary>
    /// <param name="window">Identity of the secondary window.</param>
    /// <param name="relative">Current native offset from the main window.</param>
    /// <param name="zoom">Positive zoom at which the current offset was applied.</param>
    public void Capture(PlayerWindow window, Vector2I relative, int zoom)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoom);
        if (!_offsets.TryGetValue(window, out var offset) || offset.Applied != relative)
            _offsets[window] = ((Vector2)relative / zoom, relative);
    }

    /// <summary>Calculates a native offset from the retained logical position.</summary>
    /// <param name="window">Identity captured before resizing the windows.</param>
    /// <param name="zoom">Positive destination zoom.</param>
    /// <returns>Rounded native offset to apply relative to the main window.</returns>
    public Vector2I Scale(PlayerWindow window, int zoom)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoom);
        var (logical, _) = _offsets[window];
        Vector2I scaled = (Vector2I)(logical * zoom).Round();
        _offsets[window] = (logical, scaled);
        return scaled;
    }
}
