using System;
using System.Collections.Generic;
using System.Linq;
using GodAmp.Media;
using GodAmp.Playback.Queue;
using Godot;

namespace GodAmp.Presentation.Playlist.Search;

/// <summary>Filters queue occurrences by title, artist, album and source path without changing the queue.</summary>
public partial class JumpToTrackDialog : ConfirmationDialog
{
    [Signal] public delegate void EntryChosenEventHandler(long entryId);
    [Export] private LineEdit _query = null!;
    [Export] private ItemList _results = null!;
    [Export] private Label _status = null!;
    private IReadOnlyList<QueueEntry> _entries = [];

    /// <summary>Shows search for the current queue while retaining the query between openings.</summary>
    /// <param name="entries">Live read-only queue; results use occurrence identities.</param>
    public void Open(IReadOnlyList<QueueEntry> entries)
    {
        _entries = entries;
        Refresh();
        PopupCentered();
        _query.GrabFocus();
        _query.SelectAll();
    }

    /// <summary>Refreshes matching rows and preserves the selected occurrence when it survives.</summary>
    public void Refresh()
    {
        int[] selected = _results.GetSelectedItems();
        long? selectedId = selected.Length > 0 ? _results.GetItemMetadata(selected[0]).AsInt64() : null;
        string[] terms = _query.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _results.Clear();
        for (int index = 0; index < _entries.Count; index++)
        {
            QueueEntry entry = _entries[index];
            string searchable = $"{TrackTitle.Format(entry.Track)}\n{entry.Track.Name}\n{entry.Track.Artist}\n{entry.Track.Album}\n{entry.Track.SourcePath}";
            if (!terms.All(term => searchable.Contains(term, StringComparison.OrdinalIgnoreCase)))
                continue;
            int row = _results.AddItem(TrackTitle.FormatNumbered(entry.Track, index + 1));
            _results.SetItemMetadata(row, entry.Id);
            _results.SetItemTooltip(row, entry.Track.SourcePath);
            if (selectedId == entry.Id)
                _results.Select(row);
        }
        if (_results.ItemCount > 0 && _results.GetSelectedItems().Length == 0)
            _results.Select(0);
        _status.Text = $"{_results.ItemCount} of {_entries.Count} tracks";
        GetOkButton().Disabled = _results.ItemCount == 0;
    }

    private void OnQueryChanged(string _) => Refresh();
    private void OnQuerySubmitted(string _) => ActivateSelected();
    private void OnItemActivated(long _) => ActivateSelected();

    private void ActivateSelected()
    {
        int[] selected = _results.GetSelectedItems();
        if (selected.Length == 0)
            return;
        long id = _results.GetItemMetadata(selected[0]).AsInt64();
        if (!_entries.Any(entry => entry.Id == id))
        {
            Refresh();
            return;
        }
        EmitSignal(SignalName.EntryChosen, id);
        Hide();
    }

    private void OnQueryInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key || key.Keycode is not (Key.Up or Key.Down) || _results.ItemCount == 0)
            return;
        int[] selected = _results.GetSelectedItems();
        int index = selected.Length > 0 ? selected[0] : 0;
        _results.Select(Math.Clamp(index + (key.Keycode == Key.Down ? 1 : -1), 0, _results.ItemCount - 1));
        _results.EnsureCurrentIsVisible();
        _query.AcceptEvent();
    }
}
