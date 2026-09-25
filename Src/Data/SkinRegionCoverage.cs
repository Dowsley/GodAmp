using System.Collections.Generic;
using Godot;

namespace GodAmp.Data;

/// <summary>Immutable native-pixel coverage shared by skin rendering and input hit testing.</summary>
public sealed class SkinRegionCoverage
{
    private readonly byte[] _pixels;

    /// <summary>Gets the native client dimensions represented by the coverage.</summary>
    public Vector2I Size { get; }

    /// <summary>Takes ownership of validated coverage produced by the region rasterizer.</summary>
    /// <param name="size">Native client dimensions matching the pixel buffer.</param>
    /// <param name="pixels">Exclusive row-major coverage buffer that is not modified after construction.</param>
    internal SkinRegionCoverage(Vector2I size, byte[] pixels)
    {
        Size = size;
        _pixels = pixels;
    }

    /// <summary>Tests a native client pixel without treating empty coverage as a rectangular fallback.</summary>
    /// <param name="pixel">Position relative to the native client area's top-left corner.</param>
    /// <returns>True only for an in-bounds pixel covered by the source contours.</returns>
    public bool Contains(Vector2I pixel) => pixel.X >= 0 && pixel.Y >= 0 &&
        pixel.X < Size.X && pixel.Y < Size.Y && _pixels[pixel.Y * Size.X + pixel.X] != 0;

    /// <summary>Creates an independent texture source from the coverage pixels.</summary>
    /// <returns>An L8 image with white coverage and black cutouts.</returns>
    public Image CreateImage() => Image.CreateFromData(Size.X, Size.Y, false, Image.Format.L8, _pixels);

    /// <summary>Combines identical filled spans on adjacent rows into disjoint rectangles.</summary>
    /// <param name="limit">Maximum rectangle count accepted while preparing an outline.</param>
    /// <returns>Exact coverage rectangles, or null when the representation exceeds the limit.</returns>
    internal List<Rect2I>? GetRectangles(int limit)
    {
        var rectangles = new List<Rect2I>();
        var previous = new Dictionary<(int Start, int End), int>();
        var current = new Dictionary<(int Start, int End), int>();
        for (int y = 0; y < Size.Y; y++)
        {
            current.Clear();
            int x = 0;
            while (x < Size.X)
            {
                if (_pixels[y * Size.X + x] == 0)
                {
                    x++;
                    continue;
                }
                int start = x;
                while (x < Size.X && _pixels[y * Size.X + x] != 0)
                    x++;
                var span = (start, x);
                if (previous.TryGetValue(span, out int index))
                {
                    Rect2I rectangle = rectangles[index];
                    rectangle.Size += Vector2I.Down;
                    rectangles[index] = rectangle;
                }
                else
                {
                    if (rectangles.Count >= limit)
                        return null;
                    index = rectangles.Count;
                    rectangles.Add(new Rect2I(start, y, x - start, 1));
                }
                current.Add(span, index);
            }
            (previous, current) = (current, previous);
        }
        return rectangles;
    }
}
