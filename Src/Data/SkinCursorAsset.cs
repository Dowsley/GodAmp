using System;
using Godot;

namespace GodAmp.Data;

/// <summary>Inspector-configured artwork, hotspot, and roles for a built-in cursor.</summary>
[GlobalClass]
public partial class SkinCursorAsset : Resource
{
    /// <summary>Cursor artwork at its original pixel dimensions.</summary>
    [Export] public Texture2D Texture { get; set; } = null!;
    /// <summary>Pointer input position measured from the image's top-left pixel.</summary>
    [Export] public Vector2I Hotspot { get; set; }
    /// <summary>Classic cursor roles sharing this built-in asset.</summary>
    [Export] public Godot.Collections.Array<SkinCursorRole> Roles { get; set; } = [];

    /// <summary>Reads the configured texture and validates its image-relative hotspot.</summary>
    /// <returns>A cursor ready for the native cursor controller.</returns>
    /// <exception cref="InvalidOperationException">The asset has no image or an invalid hotspot.</exception>
    public SkinCursor CreateCursor()
    {
        Image? image = Texture?.GetImage();
        if (image == null || image.IsEmpty() || Hotspot.X < 0 || Hotspot.Y < 0 ||
            Hotspot.X >= image.GetWidth() || Hotspot.Y >= image.GetHeight())
            throw new InvalidOperationException($"Invalid built-in cursor asset: {ResourcePath}");
        return new SkinCursor(image, Hotspot);
    }
}
