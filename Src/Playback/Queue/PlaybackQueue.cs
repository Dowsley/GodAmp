using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GodAmp.Media;

namespace GodAmp.Playback.Queue;

/// <summary>Owns occurrence identity, visible ordering, selection and shuffle/repeat navigation.</summary>
public sealed class PlaybackQueue
{
    private readonly List<QueueEntry> _entries = [];
    private readonly List<long> _shuffleOrder = [];
    private readonly ReadOnlyCollection<QueueEntry> _readOnlyEntries;
    private long _lastEntryId;

    /// <summary>Creates an empty queue with sequential navigation and repeat disabled.</summary>
    public PlaybackQueue() => _readOnlyEntries = _entries.AsReadOnly();

    /// <summary>Occurrences in visible order, exposed without collection mutation.</summary>
    public IReadOnlyList<QueueEntry> Entries => _readOnlyEntries;
    /// <summary>Selected occurrence, or null when empty.</summary>
    public QueueEntry? CurrentEntry { get; private set; }
    /// <summary>Selected occurrence's visible index, or -1 when empty.</summary>
    public int CurrentIndex => CurrentEntry is { } entry ? _entries.FindIndex(e => e.Id == entry.Id) : -1;
    /// <summary>Whether navigation follows a shuffled occurrence order.</summary>
    public bool ShuffleEnabled { get; private set; }
    /// <summary>Whether navigation wraps at the ends of its active order.</summary>
    public bool RepeatEnabled { get; private set; }

    /// <summary>Appends fresh occurrences while preserving existing selection and shuffle order.</summary>
    /// <param name="tracks">Tracks in visible order; repeated references remain distinct occurrences.</param>
    /// <returns>Whether any entries were added.</returns>
    public bool Append(IEnumerable<Track> tracks)
    {
        List<QueueEntry> additions = CreateEntries(tracks);
        if (additions.Count == 0) return false;
        _entries.AddRange(additions);
        if (CurrentEntry == null)
        {
            CurrentEntry = _entries[0];
            RebuildShuffleOrder();
        }
        else if (ShuffleEnabled)
            _shuffleOrder.AddRange(ShuffleIds(additions.Select(e => e.Id)));
        return true;
    }

    /// <summary>Replaces every occurrence with a fresh identity and selects the first.</summary>
    /// <param name="tracks">Replacement order; an empty sequence clears the selection.</param>
    public void Replace(IEnumerable<Track> tracks)
    {
        List<QueueEntry> replacements = CreateEntries(tracks);
        _entries.Clear();
        _entries.AddRange(replacements);
        CurrentEntry = _entries.FirstOrDefault();
        RebuildShuffleOrder();
    }

    /// <summary>Selects an existing occurrence without rebuilding shuffle order.</summary>
    /// <param name="entryId">Occurrence identity, including identities outside the visible selection.</param>
    /// <returns>Whether the identity exists; stale IDs leave selection unchanged.</returns>
    public bool Select(long entryId)
    {
        QueueEntry? entry = _entries.Find(e => e.Id == entryId);
        if (entry == null) return false;
        CurrentEntry = entry;
        return true;
    }

    /// <summary>Finds an occurrence without changing selection.</summary>
    /// <param name="entryId">Occurrence identity.</param>
    /// <returns>The surviving entry, or null for a stale identity.</returns>
    public QueueEntry? Find(long entryId) => _entries.Find(e => e.Id == entryId);

    /// <summary>Removes occurrences and selects the next survivor, or nearest predecessor, if needed.</summary>
    /// <param name="entryIds">Occurrence identities to remove; stale IDs are ignored.</param>
    /// <returns>Whether the visible queue changed.</returns>
    public bool Remove(IEnumerable<long> entryIds)
    {
        HashSet<long> removed = [.. entryIds];
        if (!_entries.Exists(e => removed.Contains(e.Id))) return false;
        if (CurrentEntry != null && removed.Contains(CurrentEntry.Id))
        {
            List<long> order = NavigationOrder();
            int index = order.IndexOf(CurrentEntry.Id);
            long? successor = order.Skip(index + 1).Concat(order.Take(index).Reverse())
                .Where(id => !removed.Contains(id)).Select(id => (long?)id).FirstOrDefault();
            CurrentEntry = _entries.Find(e => e.Id == successor);
        }
        _entries.RemoveAll(e => removed.Contains(e.Id));
        _shuffleOrder.RemoveAll(removed.Contains);
        return true;
    }

    /// <summary>Crops to surviving identities in their existing order; an empty set clears the queue.</summary>
    /// <param name="entryIds">Identities to retain; an entirely stale nonempty set is ignored.</param>
    /// <returns>Whether any occurrences were removed.</returns>
    public bool Retain(IEnumerable<long> entryIds)
    {
        HashSet<long> retained = [.. entryIds];
        if (retained.Count > 0 && !_entries.Exists(e => retained.Contains(e.Id))) return false;
        return Remove(_entries.Where(e => !retained.Contains(e.Id)).Select(e => e.Id));
    }

    /// <summary>Moves selected occurrences around a surviving target without changing navigation.</summary>
    /// <param name="entryIds">Occurrences to move in their existing visible order.</param>
    /// <param name="targetId">Surviving insertion anchor.</param>
    /// <param name="insertAfter">Whether insertion follows the anchor.</param>
    /// <returns>Whether visible order changed; invalid anchors leave it unchanged.</returns>
    public bool Move(IEnumerable<long> entryIds, long targetId, bool insertAfter)
    {
        HashSet<long> selected = [.. entryIds];
        if (selected.Contains(targetId) || Find(targetId) == null) return false;
        List<QueueEntry> moved = [.. _entries.Where(e => selected.Contains(e.Id))];
        if (moved.Count == 0) return false;
        List<QueueEntry> reordered = [.. _entries.Where(e => !selected.Contains(e.Id))];
        int targetIndex = reordered.FindIndex(e => e.Id == targetId);
        reordered.InsertRange(targetIndex + (insertAfter ? 1 : 0), moved);
        return ApplyOrder(reordered);
    }

    /// <summary>Reorders visible occurrences while retaining identity, selection and shuffle order.</summary>
    /// <param name="order">Ordering command; text comparison is case-insensitive and ordinal.</param>
    /// <returns>Whether visible order changed.</returns>
    public bool Reorder(PlaylistOrder order)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        IEnumerable<QueueEntry> ordered = order switch
        {
            PlaylistOrder.Title => _entries.OrderBy(e => TrackTitle.Format(e.Track), comparer),
            PlaylistOrder.FileName => _entries.OrderBy(e => System.IO.Path.GetFileName(e.Track.SourcePath), comparer),
            PlaylistOrder.Path => _entries.OrderBy(e => System.IO.Path.GetDirectoryName(e.Track.SourcePath), comparer)
                .ThenBy(e => System.IO.Path.GetFileName(e.Track.SourcePath), comparer),
            PlaylistOrder.Reverse => Enumerable.Reverse(_entries),
            PlaylistOrder.Randomize => _entries,
            _ => throw new ArgumentOutOfRangeException(nameof(order), order, null)
        };
        QueueEntry[] reordered = [.. ordered];
        if (order == PlaylistOrder.Randomize) Random.Shared.Shuffle(reordered);
        return ApplyOrder(reordered);
    }

    /// <summary>Finds a neighboring occurrence in the active navigation order without selecting it.</summary>
    /// <param name="direction">One for next, minus one for previous.</param>
    /// <returns>The neighbor, or null for an empty queue or a nonrepeating boundary.</returns>
    public QueueEntry? GetNeighbor(int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (CurrentEntry == null) return null;
        List<long> order = NavigationOrder();
        int index = order.IndexOf(CurrentEntry.Id) + direction;
        if (index < 0 || index >= order.Count)
        {
            if (!RepeatEnabled) return null;
            index = index < 0 ? order.Count - 1 : 0;
        }
        return Find(order[index]);
    }

    /// <summary>Changes navigation policy, anchoring a shuffled permutation at the current entry.</summary>
    /// <param name="enabled">Whether to navigate in shuffle order.</param>
    /// <returns>Whether the mode changed.</returns>
    public bool SetShuffle(bool enabled)
    {
        if (ShuffleEnabled == enabled) return false;
        ShuffleEnabled = enabled;
        RebuildShuffleOrder();
        return true;
    }

    /// <summary>Changes whether navigation wraps at either boundary.</summary>
    /// <param name="enabled">Whether to repeat the queue.</param>
    /// <returns>Whether the mode changed.</returns>
    public bool SetRepeat(bool enabled)
    {
        if (RepeatEnabled == enabled) return false;
        RepeatEnabled = enabled;
        return true;
    }

    /// <summary>Refreshes metadata for surviving targets and reports every affected shared occurrence.</summary>
    /// <param name="entryIds">Identities captured before the asynchronous read.</param>
    /// <param name="tracks">Successful metadata reads keyed by source path.</param>
    /// <returns>IDs needing presentation refresh; stale targets cannot update replacement entries.</returns>
    public long[] UpdateMetadata(IEnumerable<long> entryIds, IEnumerable<Track> tracks)
    {
        var byPath = tracks.ToDictionary(track => track.SourcePath, StringComparer.Ordinal);
        HashSet<long> targets = [.. entryIds];
        HashSet<Track> updated = [];
        foreach (QueueEntry entry in _entries.Where(entry => targets.Contains(entry.Id)))
            if (byPath.TryGetValue(entry.Track.SourcePath, out Track? metadata) && updated.Add(entry.Track))
                entry.Track.UpdateMetadata(metadata);
        return [.. _entries.Where(entry => updated.Contains(entry.Track)).Select(entry => entry.Id)];
    }

    private List<QueueEntry> CreateEntries(IEnumerable<Track> tracks) =>
        [.. tracks.Select(track => new QueueEntry(checked(++_lastEntryId), track))];

    private bool ApplyOrder(IReadOnlyCollection<QueueEntry> reordered)
    {
        if (_entries.SequenceEqual(reordered)) return false;
        _entries.Clear();
        _entries.AddRange(reordered);
        return true;
    }

    private List<long> NavigationOrder() => ShuffleEnabled ? [.. _shuffleOrder] : [.. _entries.Select(e => e.Id)];

    private void RebuildShuffleOrder()
    {
        _shuffleOrder.Clear();
        if (!ShuffleEnabled) return;
        if (CurrentEntry != null) _shuffleOrder.Add(CurrentEntry.Id);
        _shuffleOrder.AddRange(ShuffleIds(_entries.Where(e => e.Id != CurrentEntry?.Id).Select(e => e.Id)));
    }

    private static long[] ShuffleIds(IEnumerable<long> ids)
    {
        long[] shuffled = [.. ids];
        Random.Shared.Shuffle(shuffled);
        return shuffled;
    }
}
