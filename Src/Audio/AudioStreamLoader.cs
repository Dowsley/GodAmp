using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GodAmp.Media.Sources;
using Godot;

namespace GodAmp.Audio;

/// <summary>Constructs unique streams serially, transferring ownership only on successful completion.</summary>
internal sealed class AudioStreamLoader
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Reads and decodes one source outside the scene thread.</summary>
    /// <param name="path">Filesystem or packaged source path.</param>
    /// <param name="cancellation">Invalidates waiting or completed work; native decoding finishes before cleanup.</param>
    /// <returns>A uniquely owned stream that the caller must transfer to the player or dispose.</returns>
    public async Task<AudioStream> LoadAsync(string path, CancellationToken cancellation)
    {
        await _gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Load(path, cancellation), cancellation).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static AudioStream Load(string path, CancellationToken cancellation)
    {
        AudioFormat format = AudioFormats.Get(path);
        using var source = new AudioSourceStream(path, cancellation);
        if (source.Length > Array.MaxLength)
            throw new NotSupportedException("This source is too large to load into a playback stream.");
        byte[] data = new byte[(int)source.Length];
        source.ReadExactly(data, 0, data.Length);
        cancellation.ThrowIfCancellationRequested();
        AudioStream? stream = null;
        try
        {
            stream = format switch
            {
                AudioFormat.Mp3 => new AudioStreamMP3 { Data = data },
                AudioFormat.Wav => AudioStreamWav.LoadFromBuffer(data),
                AudioFormat.Ogg => AudioStreamOggVorbis.LoadFromBuffer(data),
                _ => throw new ArgumentOutOfRangeException(nameof(path))
            };
            cancellation.ThrowIfCancellationRequested();
            if (stream == null || !double.IsFinite(stream.GetLength()) || stream.GetLength() <= 0)
                throw new InvalidDataException("The source could not be decoded as supported audio.");
            return stream;
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }
}
