using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GodAmp.Data;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Audio.Playback;

/// <summary>Owns queue identity, navigation policy, and the scene's playback transport.</summary>
public partial class PlaybackController : Node
{
    [Signal] public delegate void QueueChangedEventHandler();
    [Signal] public delegate void MetadataChangedEventHandler(long[] entryIds);
    [Signal] public delegate void CurrentEntryChangedEventHandler();
    [Signal] public delegate void PlaybackStateChangedEventHandler();
    [Signal] public delegate void ModesChangedEventHandler();
    [Signal] public delegate void OpenTracksRequestedEventHandler();
    [Signal] public delegate void PlaybackFailedEventHandler(string path, AudioIssueKind kind, string message);

    [Export] private TrackPlayer _trackPlayer = null!;

    private readonly List<QueueEntry> _entries = [];
    private readonly List<long> _shuffleOrder = [];
    private readonly ReadOnlyCollection<QueueEntry> _readOnlyEntries;
    private long _lastEntryId;
    private float _pausedPosition;
    private readonly AudioStreamLoader _loader = new();
    private Task<AudioStream>? _loadTask;
    private CancellationTokenSource? _loadCancellation;
    private PlaybackState _requestedState;

    public PlaybackController() => _readOnlyEntries = _entries.AsReadOnly();

    /// <summary>Queue order exposed without access to its mutable collection.</summary>
    public IReadOnlyList<QueueEntry> Entries => _readOnlyEntries;
    public QueueEntry? CurrentEntry { get; private set; }
    public int CurrentIndex => CurrentEntry is { } entry ? _entries.FindIndex(e => e.Id == entry.Id) : -1;
    public PlaybackState State { get; private set; }
    public bool ShuffleEnabled { get; private set; }
    public bool RepeatEnabled { get; private set; }
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
        List<QueueEntry> additions = CreateEntries(tracks);
        if (additions.Count == 0)
            return;
        QueueEntry? previous = CurrentEntry;
        PlaybackState previousState = State;
        _entries.AddRange(additions);
        if (CurrentEntry == null)
        {
            LoadEntry(_entries[0], PlaybackState.Stopped);
            RebuildShuffleOrder();
        }
        else if (ShuffleEnabled)
        {
            _shuffleOrder.AddRange(ShuffleIds(additions.Select(e => e.Id)));
        }
        PublishChanges(true, previous, previousState);
    }

    /// <summary>Replaces the queue with fresh occurrences and selects the first entry stopped.</summary>
    /// <param name="tracks">Replacement order; an empty sequence clears playback.</param>
    public void Replace(IEnumerable<Track> tracks)
    {
        List<QueueEntry> replacements = CreateEntries(tracks);
        QueueEntry? previous = CurrentEntry;
        PlaybackState previousState = State;
        _entries.Clear();
        _entries.AddRange(replacements);
        LoadEntry(_entries.FirstOrDefault(), PlaybackState.Stopped);
        RebuildShuffleOrder();
        PublishChanges(true, previous, previousState);
    }

    /// <summary>Stops playback and removes every occurrence.</summary>
    public void Clear() => Replace([]);

    /// <summary>Removes occurrences, stopping if the current entry is removed.</summary>
    /// <param name="entryIds">Occurrence IDs; unknown IDs are ignored.</param>
    public void Remove(long[] entryIds) => RemoveMatching([.. entryIds]);

    /// <summary>Crops the queue to the requested occurrences in their existing order.</summary>
    /// <param name="entryIds">Occurrence IDs to retain; empty clears the queue, while an entirely stale set is ignored.</param>
    public void Retain(long[] entryIds)
    {
        HashSet<long> retained = [.. entryIds];
        if (retained.Count > 0 && !_entries.Exists(e => retained.Contains(e.Id)))
            return;
        RemoveMatching([.. _entries.Where(e => !retained.Contains(e.Id)).Select(e => e.Id)]);
    }

    /// <summary>Moves occurrences around a surviving target without changing playback or shuffle order.</summary>
    /// <param name="entryIds">Occurrences to move in their existing queue order.</param>
    /// <param name="targetId">Occurrence defining the insertion position.</param>
    /// <param name="insertAfter">Whether to insert after the target.</param>
    public void Move(long[] entryIds, long targetId, bool insertAfter)
    {
        HashSet<long> selected = [.. entryIds];
        if (selected.Contains(targetId) || !_entries.Exists(e => e.Id == targetId))
            return;
        List<QueueEntry> moved = [.. _entries.Where(e => selected.Contains(e.Id))];
        if (moved.Count == 0)
            return;
        List<QueueEntry> reordered = [.. _entries.Where(e => !selected.Contains(e.Id))];
        int targetIndex = reordered.FindIndex(e => e.Id == targetId);
        reordered.InsertRange(targetIndex + (insertAfter ? 1 : 0), moved);
        ApplyOrder(reordered);
    }

    /// <summary>Reorders existing occurrences without changing transport, identity or shuffle navigation.</summary>
    /// <param name="order">Ordering command; text keys use case-insensitive ordinal comparison.</param>
    public void Reorder(PlaylistOrder order)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        IEnumerable<QueueEntry> ordered = order switch
        {
            PlaylistOrder.Title => _entries.OrderBy(e => AudioUtils.GetTrackTitle(e.Track), comparer),
            PlaylistOrder.FileName => _entries.OrderBy(e => System.IO.Path.GetFileName(e.Track.SourcePath), comparer),
            PlaylistOrder.Path => _entries.OrderBy(e => System.IO.Path.GetDirectoryName(e.Track.SourcePath), comparer)
                .ThenBy(e => System.IO.Path.GetFileName(e.Track.SourcePath), comparer),
            PlaylistOrder.Reverse => Enumerable.Reverse(_entries),
            PlaylistOrder.Randomize => _entries,
            _ => throw new ArgumentOutOfRangeException(nameof(order), order, null)
        };
        QueueEntry[] reordered = [.. ordered];
        if (order == PlaylistOrder.Randomize)
            Random.Shared.Shuffle(reordered);
        ApplyOrder(reordered);
    }

    /// <summary>Removes unavailable local sources using the normal current-entry removal policy.</summary>
    public void RemoveMissingFiles() => RemoveMatching([.. _entries
        .Where(e => !Godot.FileAccess.FileExists(e.Track.SourcePath))
        .Select(e => e.Id)]);

    /// <summary>Applies completed metadata reads to surviving targets without changing transport or queue order.</summary>
    /// <param name="entryIds">Occurrences captured when the refresh was requested.</param>
    /// <param name="tracks">Successfully read source metadata; failed sources retain their existing metadata.</param>
    public void UpdateMetadata(long[] entryIds, IEnumerable<Track> tracks)
    {
        var byPath = tracks.ToDictionary(track => track.SourcePath, StringComparer.Ordinal);
        HashSet<long> targets = [.. entryIds];
        HashSet<Track> updated = [];
        foreach (QueueEntry entry in _entries.Where(entry => targets.Contains(entry.Id)))
        {
            if (byPath.TryGetValue(entry.Track.SourcePath, out Track? metadata) && updated.Add(entry.Track))
                entry.Track.UpdateMetadata(metadata);
        }
        if (updated.Count > 0)
            EmitSignal(SignalName.MetadataChanged, _entries.Where(entry => updated.Contains(entry.Track)).Select(entry => entry.Id).ToArray());
    }

    /// <summary>Publishes a materialized permutation only when the visible order changes.</summary>
    private void ApplyOrder(IReadOnlyCollection<QueueEntry> reordered)
    {
        if (_entries.SequenceEqual(reordered))
            return;
        _entries.Clear();
        _entries.AddRange(reordered);
        EmitSignal(SignalName.QueueChanged);
    }

    /// <summary>Starts the exact requested occurrence and continues from its position in the active order.</summary>
    /// <param name="entryId">Occurrence to activate; stale IDs are ignored.</param>
    public void Activate(long entryId)
    {
        QueueEntry? entry = _entries.Find(e => e.Id == entryId);
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
        if (ShuffleEnabled == enabled)
            return;
        ShuffleEnabled = enabled;
        RebuildShuffleOrder();
        EmitSignal(SignalName.ModesChanged);
    }

    /// <summary>Sets whether navigation wraps at the ends of the active order.</summary>
    /// <param name="enabled">Whether to repeat the queue.</param>
    public void SetRepeat(bool enabled)
    {
        if (RepeatEnabled == enabled)
            return;
        RepeatEnabled = enabled;
        EmitSignal(SignalName.ModesChanged);
    }

    /// <summary>Allocates identities before applying a queue mutation.</summary>
    private List<QueueEntry> CreateEntries(IEnumerable<Track> tracks) =>
        [.. tracks.Select(track => new QueueEntry(checked(++_lastEntryId), track))];

    /// <summary>Reconciles removal against the navigation order before discarding any entries.</summary>
    private void RemoveMatching(HashSet<long> removed)
    {
        if (!_entries.Exists(e => removed.Contains(e.Id)))
            return;
        QueueEntry? previous = CurrentEntry;
        PlaybackState previousState = State;
        QueueEntry? successor = CurrentEntry;
        bool removingCurrent = CurrentEntry != null && removed.Contains(CurrentEntry.Id);
        if (removingCurrent)
        {
            List<long> order = NavigationOrder();
            int currentIndex = order.IndexOf(CurrentEntry!.Id);
            long? successorId = order.Skip(currentIndex + 1).Concat(order.Take(currentIndex).Reverse())
                .Where(id => !removed.Contains(id)).Select(id => (long?)id).FirstOrDefault();
            successor = _entries.Find(e => e.Id == successorId);
        }
        _entries.RemoveAll(e => removed.Contains(e.Id));
        _shuffleOrder.RemoveAll(removed.Contains);
        if (removingCurrent)
            LoadEntry(successor, PlaybackState.Stopped);
        PublishChanges(true, previous, previousState);
    }

    /// <summary>Chooses a neighboring occurrence without clamping onto and restarting a boundary song.</summary>
    private void Navigate(int direction, bool finished)
    {
        if (CurrentEntry == null)
            return;
        List<long> order = NavigationOrder();
        int index = order.IndexOf(CurrentEntry.Id) + direction;
        if (index < 0 || index >= order.Count)
        {
            if (!RepeatEnabled)
            {
                if (finished)
                    Stop();
                return;
            }
            index = index < 0 ? order.Count - 1 : 0;
        }
        QueueEntry? previous = CurrentEntry;
        PlaybackState previousState = State;
        PlaybackState targetState = State == PlaybackState.Loading ? _requestedState : State;
        LoadEntry(_entries.First(e => e.Id == order[index]), finished ? PlaybackState.Playing : targetState);
        PublishChanges(false, previous, previousState);
    }

    /// <summary>Loads transport state without notifying views until the queue mutation is complete.</summary>
    private void LoadEntry(QueueEntry? entry, PlaybackState state)
    {
        bool reuseStream = CurrentEntry?.Id == entry?.Id && _trackPlayer.Stream != null;
        CancelPendingLoad();
        CurrentEntry = entry;
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
            AudioIssueKind kind = exception switch
            {
                InvalidDataException => AudioIssueKind.Decoding,
                IOException or UnauthorizedAccessException => AudioIssueKind.Access,
                NotSupportedException => AudioIssueKind.UnsupportedFormat,
                _ => AudioIssueKind.Decoding
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
        EmitSignal(SignalName.MetadataChanged, _entries.Where(entry => entry.Track == track).Select(entry => entry.Id).ToArray());
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

    private List<long> NavigationOrder() => ShuffleEnabled ? [.. _shuffleOrder] : [.. _entries.Select(e => e.Id)];

    /// <summary>Anchors a fresh shuffle permutation at the selected occurrence.</summary>
    private void RebuildShuffleOrder()
    {
        _shuffleOrder.Clear();
        if (!ShuffleEnabled)
            return;
        if (CurrentEntry != null)
            _shuffleOrder.Add(CurrentEntry.Id);
        _shuffleOrder.AddRange(ShuffleIds(_entries.Where(e => e.Id != CurrentEntry?.Id).Select(e => e.Id)));
    }

    private static long[] ShuffleIds(IEnumerable<long> ids)
    {
        long[] shuffled = [.. ids];
        Random.Shared.Shuffle(shuffled);
        return shuffled;
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
