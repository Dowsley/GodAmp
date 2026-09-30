using System;
using System.IO;
using System.Threading;
using Godot;
using FileAccess = Godot.FileAccess;

namespace GodAmp.Media.Sources;

/// <summary>Seekable, cancellable, read-only access to filesystem and packaged audio sources.</summary>
internal sealed class AudioSourceStream : Stream
{
    private const int MaximumReadBytes = 64 * 1024;
    private readonly Stream? _file;
    private readonly FileAccess? _resource;
    private readonly CancellationToken _cancellation;
    private bool _disposed;

    public AudioSourceStream(string path, CancellationToken cancellation)
    {
        _cancellation = cancellation;
        cancellation.ThrowIfCancellationRequested();
        if (path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal))
            _resource = FileAccess.Open(path, FileAccess.ModeFlags.Read)
                ?? throw new IOException($"Cannot open source ({FileAccess.GetOpenError()}).");
        else
            _file = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => !_disposed;
    public override bool CanWrite => false;
    public override long Length => _file?.Length ?? checked((long)_resource!.GetLength());
    public override long Position
    {
        get => _file?.Position ?? checked((long)_resource!.GetPosition());
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellation.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count)
            throw new ArgumentException("Read range exceeds the buffer.");
        /* Bounded reads allow cancellation between chunks and bound packaged-source scratch buffers. */
        count = Math.Min(count, MaximumReadBytes);
        if (_file != null)
            return _file.Read(buffer, offset, count);
        byte[] bytes = _resource!.GetBuffer(count);
        if (_resource.GetError() is not (Error.Ok or Error.FileEof))
            throw new IOException($"Cannot read source ({_resource.GetError()}).");
        bytes.CopyTo(buffer, offset);
        return bytes.Length;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellation.ThrowIfCancellationRequested();
        if (_file != null)
            return _file.Seek(offset, origin);
        long position = checked(offset + (origin switch
        {
            SeekOrigin.Begin => 0,
            SeekOrigin.Current => Position,
            SeekOrigin.End => Length,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        }));
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        _resource!.Seek((ulong)position);
        return Position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _file?.Dispose();
            _resource?.Dispose();
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
