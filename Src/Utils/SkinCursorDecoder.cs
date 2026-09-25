using System;
using System.Buffers.Binary;
using GodAmp.Data;
using Godot;

namespace GodAmp.Utils;

/// <summary>Decodes bounded, uncompressed Windows cursor images without temporary files.</summary>
public static class SkinCursorDecoder
{
    private const int DirectoryHeaderSize = 6;
    private const int DirectoryEntrySize = 16;
    private const int BitmapHeaderSize = 40;
    private const int MaximumDimension = 256;

    /// <summary>Reads the first supported CUR directory image, including its transparency mask.</summary>
    /// <param name="bytes">Complete CUR file contents.</param>
    /// <returns>A decoded cursor, or null for corrupt, animated, or unsupported data.</returns>
    public static SkinCursor? Decode(byte[] bytes)
    {
        ReadOnlySpan<byte> data = bytes;
        if (data.Length < DirectoryHeaderSize || U16(data, 0) != 0 || U16(data, 2) != 2)
            return null;
        int count = U16(data, 4);
        int directoryEnd = DirectoryHeaderSize + count * DirectoryEntrySize;
        if (count == 0 || directoryEnd > data.Length)
            return null;
        for (int i = 0; i < count; i++)
        {
            int entry = DirectoryHeaderSize + i * DirectoryEntrySize;
            int width = data[entry] == 0 ? MaximumDimension : data[entry];
            int height = data[entry + 1] == 0 ? MaximumDimension : data[entry + 1];
            var hotspot = new Vector2I(U16(data, entry + 4), U16(data, entry + 6));
            uint length = U32(data, entry + 8), offset = U32(data, entry + 12);
            if (hotspot.X >= width || hotspot.Y >= height || offset < directoryEnd ||
                offset > data.Length || length > data.Length - offset)
                continue;
            Image? image = DecodeBitmap(data.Slice((int)offset, (int)length), width, height);
            if (image != null)
                return new SkinCursor(image, hotspot);
        }
        return null;
    }

    /// <summary>Decodes BITMAPINFOHEADER pixels and AND transparency, rejecting destination inversion.</summary>
    /// <param name="data">One directory entry's bounded image payload.</param>
    /// <param name="width">Declared cursor width.</param>
    /// <param name="height">Declared cursor height, excluding the AND mask.</param>
    /// <returns>An RGBA image, or null for unsupported headers, compression, masks, or pixels.</returns>
    private static Image? DecodeBitmap(ReadOnlySpan<byte> data, int width, int height)
    {
        if (data.Length < BitmapHeaderSize || U32(data, 0) != BitmapHeaderSize ||
            U32(data, 4) != width || U32(data, 8) != height * 2 || U16(data, 12) != 1 || U32(data, 16) != 0)
            return null;
        int depth = U16(data, 14);
        if (depth is not (1 or 4 or 8 or 24 or 32))
            return null;
        uint colorsUsed = U32(data, 32);
        int paletteCount = depth <= 8 ? (colorsUsed == 0 ? 1 << depth : (int)Math.Min(colorsUsed, 256u)) : 0;
        if (depth <= 8 && colorsUsed > 1 << depth)
            return null;
        int pixelsOffset = BitmapHeaderSize + paletteCount * 4;
        int rowStride = ((width * depth + 31) / 32) * 4;
        int maskStride = ((width + 31) / 32) * 4;
        int maskOffset = pixelsOffset + rowStride * height;
        if (data.Length < maskOffset + maskStride * height)
            return null;

        bool hasAlpha = false;
        if (depth == 32)
        {
            for (int y = 0; y < height && !hasAlpha; y++)
                for (int x = 0; x < width; x++)
                    hasAlpha |= data[pixelsOffset + y * rowStride + x * 4 + 3] != 0;
        }
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            int sourceRow = height - 1 - y;
            for (int x = 0; x < width; x++)
            {
                int pixel = pixelsOffset + sourceRow * rowStride + x * depth / 8;
                if (depth <= 8)
                {
                    int shift = 8 - depth - (x * depth % 8);
                    int index = (data[pixel] >> shift) & ((1 << depth) - 1);
                    if (index >= paletteCount)
                        return null;
                    pixel = BitmapHeaderSize + index * 4;
                }
                byte blue = data[pixel], green = data[pixel + 1], red = data[pixel + 2];
                bool transparent = (data[maskOffset + sourceRow * maskStride + x / 8] & (0x80 >> (x % 8))) != 0;
                /* AND=1 with nonzero XOR pixels requires reading the destination framebuffer. */
                if (!hasAlpha && transparent && (red != 0 || green != 0 || blue != 0))
                    return null;
                int target = (y * width + x) * 4;
                rgba[target] = red;
                rgba[target + 1] = green;
                rgba[target + 2] = blue;
                rgba[target + 3] = hasAlpha ? data[pixel + 3] : transparent ? (byte)0 : byte.MaxValue;
            }
        }
        return Image.CreateFromData(width, height, false, Image.Format.Rgba8, rgba);
    }

    /// <summary>Reads an unsigned little-endian field after the enclosing structure is bounded.</summary>
    /// <param name="data">Validated structure bytes.</param>
    /// <param name="offset">Field offset within the structure.</param>
    /// <returns>The 16-bit field value.</returns>
    private static ushort U16(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);

    /// <summary>Reads an unsigned little-endian field after the enclosing structure is bounded.</summary>
    /// <param name="data">Validated structure bytes.</param>
    /// <param name="offset">Field offset within the structure.</param>
    /// <returns>The 32-bit field value.</returns>
    private static uint U32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
}
