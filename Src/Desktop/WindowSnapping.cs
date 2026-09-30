using System;
using System.Collections.Generic;
using Godot;

namespace GodAmp.Desktop;

/// <summary>Calculates released-window positions against an ordered set of stationary rectangles.</summary>
internal static class WindowSnapping
{
    /// <summary>Snaps opposing edges, then aligns neighboring parallel edges within the threshold.</summary>
    /// <param name="dragged">Requested desktop bounds of the dragged window.</param>
    /// <param name="neighbors">Visible stationary windows in tie-breaking priority order.</param>
    /// <param name="threshold">Exclusive maximum distance in native pixels.</param>
    /// <returns>Desktop position after snapping; dimensions are unchanged.</returns>
    public static Vector2I Snap(Rect2I dragged, IReadOnlyList<Rect2I> neighbors, int threshold)
    {
        int? bestX = null;
        int? bestY = null;
        long distanceX = long.MaxValue;
        long distanceY = long.MaxValue;
        foreach (Rect2I other in neighbors)
        {
            bool verticalOverlap = dragged.End.Y >= other.Position.Y && dragged.Position.Y <= other.End.Y;
            bool horizontalOverlap = dragged.End.X >= other.Position.X && dragged.Position.X <= other.End.X;
            if (verticalOverlap)
            {
                Consider(other.Position.X - dragged.Size.X, dragged.Position.X, threshold, ref bestX, ref distanceX);
                Consider(other.End.X, dragged.Position.X, threshold, ref bestX, ref distanceX);
            }
            if (horizontalOverlap)
            {
                Consider(other.Position.Y - dragged.Size.Y, dragged.Position.Y, threshold, ref bestY, ref distanceY);
                Consider(other.End.Y, dragged.Position.Y, threshold, ref bestY, ref distanceY);
            }
        }
        int x = bestX ?? dragged.Position.X;
        int y = bestY ?? dragged.Position.Y;
        if (bestX.HasValue)
            y = AlignEdges(dragged, neighbors, threshold, 1) ?? y;
        if (bestY.HasValue)
            x = AlignEdges(dragged, neighbors, threshold, 0) ?? x;
        return new Vector2I(x, y);
    }

    private static int? AlignEdges(Rect2I dragged, IReadOnlyList<Rect2I> neighbors, int threshold, int axis)
    {
        int? best = null;
        long distance = long.MaxValue;
        foreach (Rect2I other in neighbors)
        {
            Consider(other.Position[axis], dragged.Position[axis], threshold, ref best, ref distance);
            Consider(other.End[axis] - dragged.Size[axis], dragged.Position[axis], threshold, ref best, ref distance);
        }
        return best;
    }

    private static void Consider(int candidate, int current, int threshold, ref int? best, ref long minimum)
    {
        long distance = Math.Abs((long)candidate - current);
        if (distance >= threshold || distance >= minimum)
            return;
        minimum = distance;
        best = candidate;
    }
}
