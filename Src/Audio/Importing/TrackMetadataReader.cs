using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GodAmp.Audio.Importing;

/// <summary>Reads tags without copying audio into temporary files or retaining TagLib objects.</summary>
internal static class TrackMetadataReader
{
    /// <summary>Reads source tags, retaining filename metadata when tags cannot be interpreted.</summary>
    /// <param name="path">Filesystem or Godot resource path to a supported source.</param>
    /// <param name="cancellation">Cancels source reads and suppresses fallback results after cancellation.</param>
    /// <param name="warning">Metadata failure details, or null when tags were read successfully.</param>
    /// <returns>Plain metadata without open readers, source streams, or playback resources.</returns>
    public static ImportedTrack Read(string path, CancellationToken cancellation, out AudioIssue? warning)
    {
        warning = null;
        /* Opening first distinguishes inaccessible sources from malformed metadata. */
        using (var probe = new AudioSourceStream(path, cancellation)) { }
        try
        {
            using var source = new TagSource(path, cancellation);
            using var file = TagLib.File.Create(source);
            cancellation.ThrowIfCancellationRequested();
            var tag = file.Tag;
            var properties = file.Properties;
            bool filename = string.IsNullOrWhiteSpace(tag.Title);
            return new ImportedTrack(path, filename ? Path.GetFileNameWithoutExtension(path) : tag.Title,
                tag.FirstPerformer ?? "Unknown", tag.Album ?? "", (float)properties.Duration.TotalSeconds,
                checked((int)tag.Track), properties.AudioBitrate, properties.AudioSampleRate,
                properties.AudioChannels, filename);
        }
        catch (Exception exception) when (exception is TagLib.CorruptFileException or TagLib.UnsupportedFormatException
            or ArgumentException or OverflowException)
        {
            cancellation.ThrowIfCancellationRequested();
            warning = new AudioIssue(path, AudioIssueKind.Metadata, exception.Message);
            return new ImportedTrack(path, Path.GetFileNameWithoutExtension(path), "Unknown", "", 0, 0, 0, 0, 0, true);
        }
    }

    private sealed class TagSource(string path, CancellationToken cancellation) : TagLib.File.IFileAbstraction, IDisposable
    {
        private readonly List<Stream> _streams = [];
        public string Name => path;
        public Stream ReadStream
        {
            get
            {
                var stream = new AudioSourceStream(path, cancellation);
                _streams.Add(stream);
                return stream;
            }
        }
        public Stream WriteStream => throw new NotSupportedException("Metadata is read-only.");
        public void CloseStream(Stream stream)
        {
            stream.Dispose();
            _streams.Remove(stream);
        }
        public void Dispose()
        {
            foreach (Stream stream in _streams)
                stream.Dispose();
            _streams.Clear();
        }
    }
}
