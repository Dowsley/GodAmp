using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GodAmp.Audio;
using GodAmp.Media;
using GodAmp.Diagnostics;
using GodAmp.Playback.Queue;
using Godot;

namespace GodAmp.Playback;

/// <summary>Coordinates queue commands, asynchronous audio loading and the scene's playback transport.</summary>
public partial class PlaybackController : Node
{
    [Signal] public delegate void QueueChangedEventHandler();
    [Signal] public delegate void MetadataChangedEventHandler(long[] entryIds);
    [Signal] public delegate void CurrentEntryChangedEventHandler();
    [Signal] public delegate void PlaybackStateChangedEventHandler();
    [Signal] public delegate void ModesChangedEventHandler();
    [Signal] public delegate void OpenTracksRequestedEventHandler();
    [Signal] public delegate void PlaybackFailedEventHandler(string path, OperationIssueKind kind, string message);

    [Export] private TrackPlayer _trackPlayer = null!;

    private readonly PlaybackQueue _queue = new();
    private float _pausedPosition;
    private readonly AudioStreamLoader _loader = new();
    private Task<AudioStream>? _loadTask;
    private CancellationTokenSource? _loadCancellation;
    private PlaybackState _requestedState;

    /// <summary>Queue order exposed without access to its mutable collection.</summary>
    public IReadOnlyList<QueueEntry> Entries => _queue.Entries;
    public QueueEntry? CurrentEntry => _queue.CurrentEntry;
    public int CurrentIndex => _queue.CurrentIndex;
    public PlaybackState State { get; private set; }
    public bool ShuffleEnabled => _queue.ShuffleEnabled;
    public bool RepeatEnabled => _queue.RepeatEnabled;
    /// <summary>Decoded duration when available, otherwise the selected source's metadata duration.</summary>
    public float CurrentDuration => (float)(_trackPlayer.Stream?.GetLength() ?? CurrentEntry?.Track.Duration ?? 0);
    /// <summary>Whether a loaded, active transport can accept seek requests.</summary>
    public bool CanSeek => (State is PlaybackState.Playing or PlaybackState.Paused) && _trackPlayer.Stream != null;

    /// <summary>Current playback position, including a seek requested while paused.</summary>
    public float Position => State switch
    {
        PlaybackState.Playing => _trackPlayer.GetPlaybackPosition(),
        PlaybackState.Paused => _pausedPosition,
        _ => 0.0f
    };

    /// <summary>Appends occurrences without disturbing an existing playback session.</summary>
    /// <param name="tracks">Tracks in their intended queue order.</param>
    public void Append(IEnumerable<Track> tracks)
    {
        QueueEntry? previous = CurrentEntry;
        PlaybackState previousState = State;
        if (!_queue.Append(tracks))
            return;
        if (previous == null)
            LoadEntry(CurrentEntry, PlaybackState.Stopped);
        PublishChanges(true, previous, previousState);
    }

    /// <summary>Replaces the queue with fresh occurrences and selects the first entry stopped.</summary>
    /// <param name="tracks">Replacement order; an empty sequence clears playback.</param>
    public void Replace(IEnumerable<Track> tracks)
    {
        QueueEntry? previous = CurrentEntry;
        PlaybackState previousState = State;
        _queue.Replace(tracks);
        LoadEntry(CurrentEntry, PlaybackState.Stopped);
        PublishChanges(true, previous, previousState);
    }

    /// <summary>Stops playback and removes every occurrence.</summary>
    public void Clear() => Replace([]);

    /// <summary>Removes occurrences, stopping if the current entry is removed.</summary>
    /// <param name="entryIds">Occurrence IDs; unknown IDs are ignored.</param>
    public void Remove(long[] entryIds)
    {
        QueueEntry? previous = CurrentEntry;
        PlaybackState previousState = State;
        if (_queue.Remove(entryIds))
            ReconcileRemoval(previous, previousState);
    }

    /// <summary>Crops the queue to the requested occurrences in their existing order.</summary>
    /// <param name="entryIds">Occurrence IDs to retain; empty clears the queue, while an entirely stale set is ignored.</param>
    public void Retain(long[] entryIds)
    {
        QueueEntry? previous = CurrentEntry;
        PlaybackState previousState = State;
        if (_queue.Retain(entryIds))
            ReconcileRemoval(previous, previousState);
    }

    /// <summary>Moves occurrences around a surviving target without changing playback or shuffle order.</summary>
    /// <param name="entryIds">Occurrences to move in their existing queue order.</param>
    /// <param name="targetId">Occurrence defining the insertion position.</param>
    /// <param name="insertAfter">Whether to insert after the target.</param>
    public void Move(long[] entryIds, long targetId, bool insertAfter)
    {
        if (_queue.Move(entryIds, targetId, insertAfter))
            EmitSignal(SignalName.QueueChanged);
    }

    /// <summary>Reorders existing occurrences without changing transport, identity or shuffle navigation.</summary>
    /// <param name="order">Ordering command; text keys use case-insensitive ordinal comparison.</param>
    public void Reorder(PlaylistOrder order)
    {
        if (_queue.Reorder(order))
            EmitSignal(SignalName.QueueChanged);
    }

    /// <summary>Removes unavailable local sources using the normal current-entry removal policy.</summary>
    public void RemoveMissingFiles() => Remove([.. Entries
        .Where(e => !Godot.FileAccess.FileExists(e.Track.SourcePath))
        .Select(e => e.Id)]);

    /// <summary>Applies completed metadata reads to surviving targets without changing transport or queue order.</summary>
    /// <param name="entryIds">Occurrences captured when the refresh was requested.</param>
    /// <param name="tracks">Successfully read source metadata; failed sources retain their existing metadata.</param>
    public void UpdateMetadata(long[] entryIds, IEnumerable<Track> tracks)
    {
        long[] updated = _queue.UpdateMetadata(entryIds, tracks);
        if (updated.Length > 0)
            EmitSignal(SignalName.MetadataChanged, updated);
    }

    /// <summary>Starts the exact requested occurrence and continues from its position in the active order.</summary>
    /// <param name="entryId">Occurrence to activate; stale IDs are ignored.</param>
    public void Activate(long entryId)
    {
        QueueEntry? entry = _queue.Find(entryId);
        if (entry == null)
            return;
        QueueEntry? previous = CurrentEntry;
        PlaybackState previousState = State;
        LoadEntry(entry, PlaybackState.Playing);
        PublishChanges(false, previous, previousState);
    }

    /// <summary>Advances in the active order while preserving transport state.</summary>
    public void Next() => Navigate(1, false);

    /// <summary>Moves backward in the active order while preserving transport state.</summary>
    public void Previous() => Navigate(-1, false);

    /// <summary>Advances automatically, or stops at the final entry when repeat is disabled.</summary>
    public void OnTrackFinished()
    {
        if (State == PlaybackState.Playing)
            Navigate(1, true);
    }

    /// <summary>Starts the selected occurrence from the beginning, or requests the music picker.</summary>
    public void Play()
    {
        if (CurrentEntry == null)
        {
            EmitSignal(SignalName.OpenTracksRequested);
            return;
        }
        Activate(CurrentEntry.Id);
    }

    /// <summary>Toggles pause for an active session and applies any paused seek on resume.</summary>
    public void TogglePause()
    {
        if (State == PlaybackState.Loading)
        {
            _requestedState = _requestedState == PlaybackState.Paused ? PlaybackState.Playing : PlaybackState.Paused;
            return;
        }
        if (State == PlaybackState.Stopped)
            return;
        if (State == PlaybackState.Playing)
        {
            _pausedPosition = _trackPlayer.GetPlaybackPosition();
            _trackPlayer.StreamPaused = true;
            State = PlaybackState.Paused;
        }
        else
        {
            _trackPlayer.StreamPaused = false;
            _trackPlayer.Seek(_pausedPosition);
            State = PlaybackState.Playing;
        }
        EmitSignal(SignalName.PlaybackStateChanged);
    }

    /// <summary>Stops and resets transport while retaining the selected occurrence.</summary>
    public void Stop()
    {
        PlaybackState previous = State;
        CancelPendingLoad();
        _trackPlayer.Stop();
        _trackPlayer.StreamPaused = false;
        _pausedPosition = 0.0f;
        State = PlaybackState.Stopped;
        if (previous != State)
            EmitSignal(SignalName.PlaybackStateChanged);
    }

    /// <summary>Seeks within the current stream without changing transport state.</summary>
    /// <param name="position">Requested seconds; nonfinite values and stopped sessions are ignored.</param>
    public void Seek(float position)
    {
        if (!CanSeek || !float.IsFinite(position))
            return;
        float bounded = Mathf.Clamp(position, 0.0f, CurrentDuration);
        if (State == PlaybackState.Paused)
            _pausedPosition = bounded;
        else
            _trackPlayer.Seek(bounded);
    }

    /// <summary>Changes shuffle policy without changing the current occurrence or transport.</summary>
    /// <param name="enabled">Whether navigation follows a shuffled occurrence order.</param>
    public void SetShuffle(bool enabled)
    {
        if (_queue.SetShuffle(enabled))
            EmitSignal(SignalName.ModesChanged);
    }

    /// <summary>Sets whether navigation wraps at the ends of the active order.</summary>
    /// <param name="enabled">Whether to repeat the queue.</param>
    public void SetRepeat(bool enabled)
    {
        if (_queue.SetRepeat(enabled))
            EmitSignal(SignalName.ModesChanged);
    }

    /// <summary>Stops a removed selection before publishing reconciled queue and transport state.</summary>
    private void ReconcileRemoval(QueueEntry? previous, PlaybackState previousState)
    {
        if (previous?.Id != CurrentEntry?.Id)
            LoadEntry(CurrentEntry, PlaybackState.Stopped);
        PublishChanges(true, previous, previousState);
    }

    /// <summary>Chooses a neighboring occurrence without clamping onto and restarting a boundary song.</summary>
    private void Navigate(int direction, bool finished)
    {
        if (CurrentEntry == null)
            return;
        QueueEntry? next = _queue.GetNeighbor(direction);
        if (next == null)
        {
            if (finished)
                Stop();
            return;
        }
        QueueEntry? previous = CurrentEntry;
        PlaybackState previousState = State;
        PlaybackState targetState = State == PlaybackState.Loading ? _requestedState : State;
        LoadEntry(next, finished ? PlaybackState.Playing : targetState);
        PublishChanges(false, previous, previousState);
    }

    /// <summary>Loads transport state without notifying views until the queue mutation is complete.</summary>
    private void LoadEntry(QueueEntry? entry, PlaybackState state)
    {
        bool reuseStream = CurrentEntry?.Id == entry?.Id && _trackPlayer.Stream != null;
        CancelPendingLoad();
        if (entry != null)
            _queue.Select(entry.Id);
        _pausedPosition = 0.0f;
        _requestedState = state;
        if (entry == null || state == PlaybackState.Stopped)
        {
            _trackPlayer.ClearCurrentTrack();
            State = PlaybackState.Stopped;
            return;
        }
        if (reuseStream)
        {
            _trackPlayer.StreamPaused = false;
            _trackPlayer.Play();
            _trackPlayer.StreamPaused = state == PlaybackState.Paused;
            State = state;
            return;
        }
        _trackPlayer.ClearCurrentTrack();
        State = PlaybackState.Loading;
        _loadCancellation = new CancellationTokenSource();
        _loadTask = _loader.LoadAsync(entry.Track.SourcePath, _loadCancellation.Token);
    }

    /// <summary>Transfers completed audio to the player only while its request remains current.</summary>
    public override void _Process(double delta)
    {
        if (_loadTask is not { IsCompleted: true })
            return;
        Task<AudioStream> task = _loadTask;
        _loadTask = null;
        _loadCancellation!.Dispose();
        _loadCancellation = null;
        try
        {
            AudioStream stream = task.GetAwaiter().GetResult();
            _trackPlayer.SetCurrentTrack(CurrentEntry!.Track, stream);
            _trackPlayer.StreamPaused = _requestedState == PlaybackState.Paused;
            State = _requestedState;
        }
        catch (Exception exception)
        {
            _trackPlayer.ClearCurrentTrack();
            State = PlaybackState.Stopped;
            OperationIssueKind kind = exception switch
            {
                InvalidDataException => OperationIssueKind.Decoding,
                IOException or UnauthorizedAccessException => OperationIssueKind.Access,
                NotSupportedException => OperationIssueKind.UnsupportedFormat,
                _ => OperationIssueKind.Decoding
            };
            EmitSignal(SignalName.PlaybackFailed, CurrentEntry!.Track.SourcePath,
                (int)kind, exception.Message);
        }
        RefreshDecodedMetadata();
        EmitSignal(SignalName.PlaybackStateChanged);
    }

    /// <summary>Publishes decoded duration corrections for every occurrence sharing the current metadata.</summary>
    private void RefreshDecodedMetadata()
    {
        if (CurrentEntry == null || _trackPlayer.Stream == null)
            return;
        Track track = CurrentEntry.Track;
        float duration = CurrentDuration;
        if (Mathf.IsEqualApprox(track.Duration, duration))
            return;
        track.Duration = duration;
        EmitSignal(SignalName.MetadataChanged, Entries.Where(entry => entry.Track == track).Select(entry => entry.Id).ToArray());
    }

    private void CancelPendingLoad()
    {
        Task<AudioStream>? task = _loadTask;
        CancellationTokenSource? cancellation = _loadCancellation;
        _loadTask = null;
        _loadCancellation = null;
        cancellation?.Cancel();
        if (task == null)
        {
            cancellation?.Dispose();
            return;
        }
        /* A completed but unclaimed stream still belongs to the cancelled request. */
        _ = task.ContinueWith(completed =>
        {
            if (completed.Status == TaskStatus.RanToCompletion)
                completed.Result.Dispose();
            else
                _ = completed.Exception;
            cancellation?.Dispose();
        }, TaskScheduler.Default);
    }

    public override void _ExitTree()
    {
        CancelPendingLoad();
        _trackPlayer.ClearCurrentTrack();
    }

    /// <summary>Publishes only fully reconciled state; structural notifications precede current-entry updates.</summary>
    private void PublishChanges(bool queueChanged, QueueEntry? previous, PlaybackState previousState)
    {
        if (queueChanged)
            EmitSignal(SignalName.QueueChanged);
        if (previous?.Id != CurrentEntry?.Id)
            EmitSignal(SignalName.CurrentEntryChanged);
        if (previousState != State)
            EmitSignal(SignalName.PlaybackStateChanged);
    }
}
