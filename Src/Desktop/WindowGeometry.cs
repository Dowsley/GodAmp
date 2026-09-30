using System;
using Godot;

namespace GodAmp.Desktop;

/// <summary>Calculates window dimensions from explicit layout constraints.</summary>
internal static class WindowGeometry
{
    /// <summary>Clamps dimensions to the minimum and rounds down to the resize grid.</summary>
    /// <param name="size">Requested logical dimensions.</param>
    /// <param name="minimum">Minimum logical dimensions supported by the panel.</param>
    /// <param name="increment">Resize step on each axis; values below one use one pixel.</param>
    /// <returns>Dimensions on the panel's resize grid.</returns>
    public static Vector2I NormalizeSize(Vector2I size, Vector2I minimum, Vector2I increment)
    {
        Vector2I step = new(Math.Max(1, increment.X), Math.Max(1, increment.Y));
        Vector2I extra = new(Math.Max(0, size.X - minimum.X), Math.Max(0, size.Y - minimum.Y));
        return minimum + new Vector2I(extra.X / step.X * step.X, extra.Y / step.Y * step.Y);
    }
}
