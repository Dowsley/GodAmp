using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GodAmp.Media.Sources;

internal enum AudioFormat { Mp3, Wav, Ogg }

/// <summary>Supported source formats shared by discovery, pickers, and decoding.</summary>
internal static class AudioFormats
{
    private static readonly Dictionary<string, AudioFormat> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = AudioFormat.Mp3,
        [".wav"] = AudioFormat.Wav,
        [".ogg"] = AudioFormat.Ogg
    };

    public static string[] FileFilters => [.. Formats.Keys.Select(extension => $"*{extension}")];
    public static bool Supports(string path) => Formats.ContainsKey(Path.GetExtension(path));
    public static AudioFormat Get(string path) => Formats.TryGetValue(Path.GetExtension(path), out var format)
        ? format : throw new NotSupportedException("Supported audio formats are MP3, WAV, and OGG.");
}
