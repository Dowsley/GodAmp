using System;
using System.Collections.Generic;
using Godot;

namespace GodAmp.Utils;

/// <summary>Propagates size changes through direct desktop edge contacts without resizing neighbors.</summary>
public static class WindowDockLayout
{
    /// <summary>Finds positions after resizing an anchored window in a captured layout.</summary>
    /// <param name="bounds">Desktop rectangles in stable priority order.</param>
    /// <param name="anchor">Index of the window whose size changes.</param>
    /// <param name="size">Resulting native dimensions of the anchored window.</param>
    /// <param name="tolerance">Maximum gap between contacting edges, in desktop pixels.</param>
    /// <returns>Desktop origins, preserving unconnected windows and resolving conflicts by shortest path then input order.</returns>
    public static Vector2I[] Resize(IReadOnlyList<Rect2I> bounds, int anchor, Vector2I size, int tolerance)
    {
        var positions = new Vector2I[bounds.Count];
        var assigned = new bool[bounds.Count];
        var pending = new Queue<int>();
        for (int i = 0; i < bounds.Count; i++)
            positions[i] = bounds[i].Position;
        assigned[anchor] = true;
        pending.Enqueue(anchor);
        Vector2I growth = size - bounds[anchor].Size;
        while (pending.TryDequeue(out int current))
        {
            for (int next = 0; next < bounds.Count; next++)
            {
                if (assigned[next] || !TryGetContact(bounds[current], bounds[next], tolerance, out Vector2I movingEdges))
                    continue;
                Vector2I displacement = positions[current] - bounds[current].Position;
                if (current == anchor)
                    displacement += growth * movingEdges;
                positions[next] += displacement;
                assigned[next] = true;
                pending.Enqueue(next);
            }
        }
        return positions;
    }

    /// <summary>Identifies direct contacts with positive overlap, excluding corner-only proximity.</summary>
    /// <param name="first">Reference window bounds.</param>
    /// <param name="second">Potential neighbor bounds.</param>
    /// <param name="tolerance">Maximum native-pixel gap between opposing edges.</param>
    /// <param name="movingEdges">Unit components identifying contacts on the reference window's right or bottom edge.</param>
    /// <returns>True when the rectangles share a horizontal or vertical edge contact.</returns>
    public static bool TryGetContact(Rect2I first, Rect2I second, int tolerance, out Vector2I movingEdges)
    {
        movingEdges = Vector2I.Zero;
        bool verticalOverlap = Math.Max(first.Position.Y, second.Position.Y) < Math.Min(first.End.Y, second.End.Y);
        bool horizontalOverlap = Math.Max(first.Position.X, second.Position.X) < Math.Min(first.End.X, second.End.X);
        if (verticalOverlap && Math.Abs((long)first.End.X - second.Position.X) <= tolerance)
        {
            movingEdges.X = 1;
            return true;
        }
        if (verticalOverlap && Math.Abs((long)second.End.X - first.Position.X) <= tolerance)
            return true;
        if (horizontalOverlap && Math.Abs((long)first.End.Y - second.Position.Y) <= tolerance)
        {
            movingEdges.Y = 1;
            return true;
        }
        return horizontalOverlap && Math.Abs((long)second.End.Y - first.Position.Y) <= tolerance;
    }
}
