using System;
using System.Buffers.Binary;
using System.IO;
using Godot;

namespace GodAmp.Utils;

/// <summary>Bounds image dimensions before handing skin artwork to native decoders.</summary>
internal static class SkinImageHeader
{
    private const long MaximumPixels = 4 * 1024 * 1024;
    private const int BitmapFileHeaderSize = 14;
    private const int BitmapCoreHeaderSize = 12;
    private const int BitmapInfoHeaderSize = 40;

    /// <summary>Validates BMP storage and expands a shortened indexed palette for the native decoder.</summary>
    /// <param name="bytes">Dimension-validated BMP payload.</param>
    /// <param name="name">Asset path included in diagnostics.</param>
    /// <returns>The original payload or an equivalent BMP with a full indexed color table.</returns>
    /// <exception cref="InvalidDataException">The encoding, palette, or pixel storage is invalid or unsupported.</exception>
    public static byte[] PrepareBitmap(byte[] bytes, string name)
    {
        const int paletteEntryBytes = 4;
        if (bytes.Length < BitmapFileHeaderSize + BitmapInfoHeaderSize)
            throw new InvalidDataException($"Unsupported BMP header in '{name}'.");
        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(14));
        uint fileSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(2));
        uint pixelOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(10));
        int width = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(18));
        int height = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(22));
        ushort planes = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26));
        ushort depth = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28));
        uint compression = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(30));
        if (headerSize < BitmapInfoHeaderSize || headerSize > bytes.Length - BitmapFileHeaderSize ||
            width <= 0 || height <= 0 || planes != 1 || compression != 0 || depth is not (1 or 4 or 8 or 24 or 32))
            throw new InvalidDataException($"Unsupported BMP encoding in '{name}'; expected bottom-up, uncompressed 1/4/8/24/32-bit artwork.");
        long stride = ((long)width * depth + 31) / 32 * 4;
        if (pixelOffset < BitmapFileHeaderSize + headerSize || fileSize > bytes.Length ||
            pixelOffset + stride * height > fileSize)
            throw new InvalidDataException($"Truncated or invalid BMP pixel storage in '{name}'.");
        if (depth > 8)
            return bytes;
        int paletteStart = BitmapFileHeaderSize + (int)headerSize;
        int capacity = 1 << depth;
        uint used = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(46));
        int colors = used == 0 ? capacity : used <= capacity ? (int)used : 0;
        if (colors == 0 || paletteStart + colors * paletteEntryBytes > pixelOffset)
            throw new InvalidDataException($"Truncated or invalid BMP palette in '{name}'.");
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int packed = bytes[(int)(pixelOffset + y * stride + (long)x * depth / 8)];
                int index = (packed >> (8 - depth - x * depth % 8)) & (capacity - 1);
                if (index >= colors)
                    throw new InvalidDataException($"BMP pixel references a missing palette color in '{name}'.");
            }
        int padding = Math.Max(0, paletteStart + capacity * paletteEntryBytes - (int)pixelOffset);
        if (padding == 0)
            return bytes;
        /* Godot reads a full color table even when biClrUsed specifies fewer entries. */
        var normalized = new byte[bytes.Length + padding];
        bytes.AsSpan(0, (int)pixelOffset).CopyTo(normalized);
        bytes.AsSpan((int)pixelOffset).CopyTo(normalized.AsSpan((int)pixelOffset + padding));
        BinaryPrimitives.WriteUInt32LittleEndian(normalized.AsSpan(2), fileSize + (uint)padding);
        BinaryPrimitives.WriteUInt32LittleEndian(normalized.AsSpan(10), pixelOffset + (uint)padding);
        return normalized;
    }

    /// <summary>Validates the encoded dimensions against sprite requirements and the decoded-memory budget.</summary>
    /// <param name="bytes">Encoded image payload.</param>
    /// <param name="extension">Lowercase BMP, PNG, JPG, or JPEG extension.</param>
    /// <param name="minimum">Smallest usable sprite sheet dimensions.</param>
    /// <param name="name">Asset path included in diagnostics.</param>
    /// <exception cref="InvalidDataException">The header is invalid or dimensions are outside supported bounds.</exception>
    public static void Validate(ReadOnlySpan<byte> bytes, string extension, Vector2I minimum, string name)
    {
        (long width, long height) = extension switch
        {
            ".bmp" => ReadBitmap(bytes),
            ".png" => ReadPng(bytes),
            _ => ReadJpeg(bytes)
        };
        if (width < minimum.X || height < minimum.Y || width * height > MaximumPixels)
            throw new InvalidDataException($"Invalid dimensions for skin image '{name}': {width}x{height}; " +
                $"expected at least {minimum.X}x{minimum.Y} and at most {MaximumPixels} pixels.");
    }

    /// <summary>Reads Windows or OS/2 BMP dimensions without allocating pixel storage.</summary>
    /// <param name="bytes">Complete BMP payload.</param>
    /// <returns>Signed width and absolute height, or zero dimensions for an invalid header.</returns>
    private static (long Width, long Height) ReadBitmap(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < BitmapFileHeaderSize + BitmapCoreHeaderSize || !bytes[..2].SequenceEqual("BM"u8))
            return default;
        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[14..]);
        if (headerSize > bytes.Length - BitmapFileHeaderSize)
            return default;
        if (headerSize == BitmapCoreHeaderSize)
            return (BinaryPrimitives.ReadUInt16LittleEndian(bytes[18..]), BinaryPrimitives.ReadUInt16LittleEndian(bytes[20..]));
        if (headerSize < BitmapInfoHeaderSize)
            return default;
        return (BinaryPrimitives.ReadInt32LittleEndian(bytes[18..]),
            Math.Abs((long)BinaryPrimitives.ReadInt32LittleEndian(bytes[22..])));
    }

    /// <summary>Reads dimensions from the required first PNG IHDR chunk.</summary>
    /// <param name="bytes">Complete PNG payload.</param>
    /// <returns>Header dimensions, or zero dimensions when the signature or IHDR is invalid.</returns>
    private static (long Width, long Height) ReadPng(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 33 || !bytes[..8].SequenceEqual(signature) ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]) != 13 || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
            return default;
        uint width = BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]);
        uint height = BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]);
        return width <= int.MaxValue && height <= int.MaxValue ? (width, height) : default;
    }

    /// <summary>Walks bounded JPEG marker segments up to the frame header, before entropy-coded scan data.</summary>
    /// <param name="bytes">Complete JPEG payload.</param>
    /// <returns>Frame dimensions, or zero dimensions when no valid frame header precedes scan data.</returns>
    private static (long Width, long Height) ReadJpeg(ReadOnlySpan<byte> bytes)
    {
        const byte markerPrefix = 0xff;
        const byte startOfImage = 0xd8;
        const byte endOfImage = 0xd9;
        const byte startOfScan = 0xda;
        if (bytes.Length < 2 || bytes[0] != markerPrefix || bytes[1] != startOfImage)
            return default;
        int offset = 2;
        while (offset < bytes.Length)
        {
            if (bytes[offset++] != markerPrefix)
                return default;
            while (offset < bytes.Length && bytes[offset] == markerPrefix)
                offset++;
            if (offset >= bytes.Length)
                return default;
            byte marker = bytes[offset++];
            if (marker is startOfScan or endOfImage || offset > bytes.Length - 2)
                return default;
            int length = BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);
            if (length < 2 || length > bytes.Length - offset)
                return default;
            /* SOF markers occupy C0-CF, excluding Huffman tables, extensions, and arithmetic conditioning. */
            if (marker is >= 0xc0 and <= 0xcf && marker is not (0xc4 or 0xc8 or 0xcc))
                return length >= 8
                    ? (BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 5)..]),
                        BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 3)..])) : default;
            offset += length;
        }
        return default;
    }
}
