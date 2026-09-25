using System;
using System.Collections.Generic;
using Godot;

namespace GodAmp.Utils;

/// <summary>Prepares bounded simple window contours and binary coverage masks.</summary>
public static class SkinRegionGeometry
{
    /* Bound CPU work and texture allocation for externally supplied skin data. */
    private const int MaximumVertices = 1024;
    private const int MaximumMaskPixels = 16 * 1024 * 1024;

    /// <summary>Transforms and validates one simple contour without changing its winding.</summary>
    /// <param name="contours">Source polygons in panel coordinates.</param>
    /// <param name="transform">Panel-to-native-window transform.</param>
    /// <param name="size">Native client area in pixels.</param>
    /// <returns>A supported polygon, or an empty array for unsupported or invalid geometry.</returns>
    public static Vector2[] Prepare(IReadOnlyList<IReadOnlyList<Vector2I>> contours,
        Transform2D transform, Vector2I size)
    {
        if (contours.Count != 1 || contours[0].Count > MaximumVertices ||
            size.X <= 0 || size.Y <= 0 || (long)size.X * size.Y > MaximumMaskPixels)
            return [];
        var points = new List<Vector2>();
        foreach (Vector2I source in contours[0])
        {
            Vector2 point = transform * (Vector2)source;
            if (!point.IsFinite())
                return [];
            if (points.Count == 0 || points[^1] != point)
                points.Add(point);
        }
        if (points.Count > 1 && points[0] == points[^1])
            points.RemoveAt(points.Count - 1);
        if (points.Count < 3)
            return [];

        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Count];
            Vector2 c = points[(i + 2) % points.Count];
            area += Cross(a, b);
            if (Cross(b - a, c - b) == 0 && (b - a).Dot(c - b) < 0)
                return [];
            for (int j = i + 2; j < points.Count; j++)
            {
                if (i == 0 && j == points.Count - 1)
                    continue;
                if (Intersects(a, b, points[j], points[(j + 1) % points.Count]))
                    return [];
            }
        }
        if (area == 0)
            return [];
        Vector2[] bounds = [Vector2.Zero, new(size.X, 0), size, new(0, size.Y)];
        var clipped = Geometry2D.IntersectPolygons(points.ToArray(), bounds);
        return clipped.Count == 1 && clipped[0].Length >= 3 ? clipped[0] : [];
    }

    /// <summary>Rasterizes a validated simple polygon at pixel centers without antialiasing.</summary>
    /// <param name="polygon">A contour returned by <see cref="Prepare"/>.</param>
    /// <param name="size">Validated native client dimensions used during preparation.</param>
    /// <returns>An L8 image containing white coverage and black cutouts.</returns>
    public static Image CreateMask(Vector2[] polygon, Vector2I size)
    {
        var pixels = new byte[checked(size.X * size.Y)];
        var crossings = new List<float>(polygon.Length);
        for (int y = 0; y < size.Y; y++)
        {
            float scanY = y + 0.5f;
            crossings.Clear();
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                if ((a.Y > scanY) != (b.Y > scanY))
                    crossings.Add(a.X + (scanY - a.Y) * (b.X - a.X) / (b.Y - a.Y));
            }
            crossings.Sort();
            for (int i = 0; i + 1 < crossings.Count; i += 2)
            {
                int start = Math.Clamp((int)Math.Ceiling(crossings[i] - 0.5f), 0, size.X);
                int end = Math.Clamp((int)Math.Ceiling(crossings[i + 1] - 0.5f), 0, size.X);
                Array.Fill(pixels, byte.MaxValue, y * size.X + start, end - start);
            }
        }
        return Image.CreateFromData(size.X, size.Y, false, Image.Format.L8, pixels);
    }

    /// <summary>Computes the signed 2D cross product with double-precision intermediates.</summary>
    /// <param name="a">First vector.</param>
    /// <param name="b">Second vector.</param>
    /// <returns>The signed area of the parallelogram.</returns>
    private static double Cross(Vector2 a, Vector2 b) => (double)a.X * b.Y - (double)a.Y * b.X;

    /// <summary>Detects crossings and touching or overlapping nonadjacent edges.</summary>
    /// <param name="a">First edge start.</param>
    /// <param name="b">First edge end.</param>
    /// <param name="c">Second edge start.</param>
    /// <param name="d">Second edge end.</param>
    /// <returns>True when the closed segments share any point.</returns>
    private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (Math.Max(a.X, b.X) < Math.Min(c.X, d.X) || Math.Max(c.X, d.X) < Math.Min(a.X, b.X) ||
            Math.Max(a.Y, b.Y) < Math.Min(c.Y, d.Y) || Math.Max(c.Y, d.Y) < Math.Min(a.Y, b.Y))
            return false;
        return Math.Sign(Cross(b - a, c - a)) * Math.Sign(Cross(b - a, d - a)) <= 0 &&
            Math.Sign(Cross(d - c, a - c)) * Math.Sign(Cross(d - c, b - c)) <= 0;
    }
}
