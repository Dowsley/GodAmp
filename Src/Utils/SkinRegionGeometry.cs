using System;
using System.Collections.Generic;
using GodAmp.Data;
using Godot;

namespace GodAmp.Utils;

/// <summary>Prepares simple native contours and bounded nonzero-winding coverage masks.</summary>
public static class SkinRegionGeometry
{
    /* Bound CPU work and texture allocation for externally supplied skin data. */
    private const int MaximumVertices = 1024;
    private const int MaximumMaskPixels = 16 * 1024 * 1024;
    private const int MaximumScanlineEdgeTests = 16 * 1024 * 1024;
    private readonly record struct Crossing(double X, int Direction);

    /// <summary>Rasterizes transformed contours with Winamp's nonzero winding fill rule.</summary>
    /// <param name="contours">Implicitly closed source contours, preserving their winding directions.</param>
    /// <param name="transform">Panel-to-native-client transform.</param>
    /// <param name="size">Native client dimensions to clip coverage against.</param>
    /// <returns>Coverage, including valid empty results, or null for absent, invalid, or excessive input.</returns>
    public static SkinRegionCoverage? CreateCoverage(IReadOnlyList<IReadOnlyList<Vector2I>> contours,
        Transform2D transform, Vector2I size)
    {
        if (contours.Count == 0 || contours.Count > MaximumVertices / 3 ||
            size.X <= 0 || size.Y <= 0 || (long)size.X * size.Y > MaximumMaskPixels)
            return null;
        int vertices = 0;
        var transformed = new List<Vector2[]>(contours.Count);
        foreach (var contour in contours)
        {
            if (contour.Count < 3 || contour.Count > MaximumVertices - vertices)
                return null;
            vertices += contour.Count;
            var points = new Vector2[contour.Count];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = transform * (Vector2)contour[i];
                if (!points[i].IsFinite())
                    return null;
            }
            transformed.Add(points);
        }
        return (long)size.Y * vertices <= MaximumScanlineEdgeTests ? Rasterize(transformed, size) : null;
    }

    /// <summary>Resolves source contours to one exact outline supported by Godot's native window API.</summary>
    /// <param name="contours">Source polygons in panel coordinates.</param>
    /// <param name="transform">Panel-to-native-window transform.</param>
    /// <param name="size">Native client area in pixels.</param>
    /// <returns>A supported polygon, or an empty array for unsupported or invalid geometry.</returns>
    public static Vector2[] Prepare(IReadOnlyList<IReadOnlyList<Vector2I>> contours,
        Transform2D transform, Vector2I size)
    {
        Vector2[] simple = PrepareSimple(contours, transform, size);
        if (simple.Length > 0)
            return simple;
        SkinRegionCoverage? coverage = CreateCoverage(contours, transform, size);
        if (coverage == null)
            return [];
        List<Rect2I>? rectangles = coverage.GetRectangles(MaximumVertices);
        if (rectangles == null || rectangles.Count == 0)
            return [];
        using var bitmap = new Bitmap();
        bitmap.Create(size);
        foreach (Rect2I rectangle in rectangles)
            bitmap.SetBitRect(rectangle, true);
        var outlines = bitmap.OpaqueToPolygons(new Rect2I(Vector2I.Zero, size), 0);
        if (outlines.Count != 1)
            return [];
        var vertices = Array.ConvertAll(outlines[0], point => (Vector2I)point);
        Vector2[] polygon = PrepareSimple([vertices], Transform2D.Identity, size);
        if (polygon.Length == 0)
            return [];
        /* Tracing can omit holes or simplify boundaries. Accept only an exact coverage match. */
        using Image expected = coverage.CreateImage();
        using Image actual = CreateMask(polygon, size);
        return expected.GetData().AsSpan().SequenceEqual(actual.GetData()) ? polygon : [];
    }

    /// <summary>Transforms and validates one simple contour without changing its winding.</summary>
    /// <param name="contours">Source polygons in panel coordinates.</param>
    /// <param name="transform">Panel-to-native-window transform.</param>
    /// <param name="size">Native client dimensions used for clipping and work limits.</param>
    /// <returns>A clipped simple outline, or an empty array when further normalization is required.</returns>
    private static Vector2[] PrepareSimple(IReadOnlyList<IReadOnlyList<Vector2I>> contours,
        Transform2D transform, Vector2I size)
    {
        if (contours.Count != 1 || contours[0].Count > MaximumVertices ||
            size.X <= 0 || size.Y <= 0 || (long)size.X * size.Y > MaximumMaskPixels ||
            (long)size.Y * contours[0].Count > MaximumScanlineEdgeTests)
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
        => Rasterize([polygon], size).CreateImage();

    /// <summary>Accumulates directed edge crossings at pixel centers, clipping only the filled spans.</summary>
    /// <param name="contours">Finite, bounded sets of transformed vertices.</param>
    /// <param name="size">Validated native client dimensions.</param>
    /// <returns>Binary coverage preserving holes, overlaps, disconnected contours, and self-intersections.</returns>
    private static SkinRegionCoverage Rasterize(IReadOnlyList<Vector2[]> contours, Vector2I size)
    {
        var pixels = new byte[checked(size.X * size.Y)];
        var crossings = new List<Crossing>();
        for (int y = 0; y < size.Y; y++)
        {
            double scanY = y + 0.5;
            crossings.Clear();
            foreach (Vector2[] polygon in contours)
                for (int i = 0; i < polygon.Length; i++)
                {
                    Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                    if ((a.Y > scanY) != (b.Y > scanY))
                    {
                        int direction = b.Y > a.Y ? 1 : -1;
                        /* Opposite traversals of a shared edge must calculate identical crossing positions. */
                        if (direction < 0)
                            (a, b) = (b, a);
                        crossings.Add(new Crossing(a.X + (scanY - a.Y) * ((double)b.X - a.X) /
                            ((double)b.Y - a.Y), direction));
                    }
                }
            crossings.Sort(static (a, b) => a.X.CompareTo(b.X));
            int winding = 0;
            for (int i = 0; i + 1 < crossings.Count; i++)
            {
                winding += crossings[i].Direction;
                if (winding == 0)
                    continue;
                int start = (int)Math.Clamp(Math.Ceiling(crossings[i].X - 0.5), 0, size.X);
                int end = (int)Math.Clamp(Math.Ceiling(crossings[i + 1].X - 0.5), 0, size.X);
                Array.Fill(pixels, byte.MaxValue, y * size.X + start, end - start);
            }
        }
        return new SkinRegionCoverage(size, pixels);
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
