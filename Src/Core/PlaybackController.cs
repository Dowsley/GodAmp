using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GodAmp.Data;
using Godot;

namespace GodAmp.Core;

/// <summary>Owns queue identity, navigation policy, and the scene's playback transport.</summary>
public partial class PlaybackController : Node
{
    [Signal] public delegate void QueueChangedEventHandler();
    [Signal] public delegate void CurrentEntryChangedEventHandler();
    [Signal] public delegate void PlaybackStateChangedEventHandler();
    [Signal] public delegate void ModesChangedEventHandler();
    [Signal] public delegate void OpenTracksRequestedEventHandler();

    [Export] private TrackPlayer _trackPlayer = null!;

    private readonly List<QueueEntry> _entries = [];
    private readonly List<long> _shuffleOrder = [];
    private readonly ReadOnlyCollection<QueueEntry> _readOnlyEntries;
    private long _lastEntryId;
    private float _pausedPosition;

    public PlaybackController() => _readOnlyEntries = _entries.AsReadOnly();

    /// <summary>Queue order exposed without access to its mutable collection.</summary>
    public IReadOnlyList<QueueEntry> Entries => _readOnlyEntries;
    public QueueEntry? CurrentEntry { get; private set; }
    public int CurrentIndex => CurrentEntry is { } entry ? _entries.FindIndex(e => e.Id == entry.Id) : -1;
    public PlaybackState State { get; private set; }
    public bool ShuffleEnabled { get; private set; }
    public bool RepeatEnabled { get; private set; }

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

    /// <summary>Replaces the queue with fresh occurrences and loads the first entry stopped.</summary>
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
        if (State == PlaybackState.Stopped || CurrentEntry == null || !float.IsFinite(position))
            return;
        float bounded = Mathf.Clamp(position, 0.0f, (float)CurrentEntry.Track.Stream.GetLength());
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
        LoadEntry(_entries.First(e => e.Id == order[index]), finished ? PlaybackState.Playing : State);
        PublishChanges(false, previous, previousState);
    }

    /// <summary>Loads transport state without notifying views until the queue mutation is complete.</summary>
    private void LoadEntry(QueueEntry? entry, PlaybackState state)
    {
        CurrentEntry = entry;
        _pausedPosition = 0.0f;
        if (entry == null)
        {
            _trackPlayer.ClearCurrentTrack();
            State = PlaybackState.Stopped;
            return;
        }
        _trackPlayer.SetCurrentTrack(entry.Track, state != PlaybackState.Stopped);
        _trackPlayer.StreamPaused = state == PlaybackState.Paused;
        State = state;
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
