using System;
using System.Collections.Generic;
using System.Linq;
using GodAmp.Autoload;
using GodAmp.Components;
using GodAmp.Audio.Playback;
using GodAmp.Data;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.Playlist;

/// <summary>Displays the shared queue and owns selection independently of playback.</summary>
public partial class Playlist : WindowPanelContainer
{
    [Signal] public delegate void EntryActivatedEventHandler(long entryId);
    [Signal] public delegate void RemoveEntriesRequestedEventHandler(long[] entryIds);
    [Signal] public delegate void RetainEntriesRequestedEventHandler(long[] entryIds);
    [Signal] public delegate void ClearRequestedEventHandler();
    [Signal] public delegate void FilesRequestedEventHandler(bool replace);
    [Signal] public delegate void FolderRequestedEventHandler(bool replace);
    [Signal] public delegate void LoadPlaylistRequestedEventHandler();
    [Signal] public delegate void SavePlaylistRequestedEventHandler();
    [Signal] public delegate void MoveEntriesRequestedEventHandler(long[] entryIds, long targetId, bool insertAfter);

    [ExportGroup("Config")]
    [Export] public PackedScene TrackLabelScene = null!;

    [ExportGroup("References")]
    [Export] private PlaybackController _playbackController = null!;
    [ExportSubgroup("Controls")]
    [Export] private VBoxContainer _trackEntryContainer = null!;
    [Export] private ScrollContainer _scrollContainer = null!;
    [Export] private Label _windowshadeTitle = null!;
    [Export] private Label _windowshadeDuration = null!;

    [ExportSubgroup("Button dropdowns")]
    [Export] public ButtonDropdown AddButtonDropdown = null!;
    [Export] public ButtonDropdown RemoveButtonDropdown = null!;
    [Export] public ButtonDropdown SelectButtonDropdown = null!;
    [Export] public ButtonDropdown MiscButtonDropdown = null!;
    [Export] public ButtonDropdown ListOptionsButtonDropdown = null!;

    [ExportSubgroup("Buttons")]
    [Export] public TextureButton AddButton = null!;
    [Export] public TextureButton RemoveButton = null!;
    [Export] public TextureButton SelectButton = null!;
    [Export] public TextureButton MiscButton = null!;
    [Export] public TextureButton ListOptionsButton = null!;

    private readonly HashSet<long> _selectedEntries = [];
    private long? _selectionAnchorId;

    /// <inheritdoc />
    public override void _Ready()
    {
        base._Ready();
        SignalBus.Instance.SkinChanged += OnSkinChanged;
        OnSkinChanged();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        SignalBus.Instance.SkinChanged -= OnSkinChanged;
        DisconnectRows();
        base._ExitTree();
    }

    /// <summary>Rebuilds rows from queue state while retaining surviving selection identities.</summary>
    public void Refresh()
    {
        _selectedEntries.IntersectWith(_playbackController.Entries.Select(e => e.Id));
        if (_selectionAnchorId is { } anchor && EntryIndex(anchor) < 0)
            _selectionAnchorId = null;
        DisconnectRows();
        foreach (Node child in _trackEntryContainer.GetChildren())
        {
            _trackEntryContainer.RemoveChild(child);
            child.QueueFree();
        }

        for (int i = 0; i < _playbackController.Entries.Count; i++)
        {
            QueueEntry entry = _playbackController.Entries[i];
            var row = TrackLabelScene.Instantiate<PlaylistTrackEntry>();
            _trackEntryContainer.AddChild(row);
            row.Selected += OnEntrySelected;
            row.Activated += OnEntryActivated;
            row.MoveRequested += OnMoveRequested;
            row.Setup(AudioUtils.GetFullTrackTitle(entry.Track, i + 1), entry.Track.Duration,
                entry.Id, _selectedEntries.Contains(entry.Id), entry.Id == _playbackController.CurrentEntry?.Id);
        }
        RefreshCurrentEntry();
    }

    /// <summary>Updates current-entry presentation without changing UI selection.</summary>
    public void RefreshCurrentEntry()
    {
        QueueEntry? current = _playbackController.CurrentEntry;
        _windowshadeTitle.Text = current == null ? "" : AudioUtils.GetFullTrackTitle(current.Track, _playbackController.CurrentIndex + 1);
        _windowshadeDuration.Text = current == null ? "" : TimeUtils.FormatAsTrackTime(current.Track.Duration);
        foreach (PlaylistTrackEntry row in _trackEntryContainer.GetChildren().OfType<PlaylistTrackEntry>())
            row.SetCurrent(row.EntryId == current?.Id);
    }

    /// <summary>Disconnects subscriptions owned by dynamically instantiated rows.</summary>
    private void DisconnectRows()
    {
        foreach (PlaylistTrackEntry row in _trackEntryContainer.GetChildren().OfType<PlaylistTrackEntry>())
        {
            row.Selected -= OnEntrySelected;
            row.Activated -= OnEntryActivated;
            row.MoveRequested -= OnMoveRequested;
        }
    }

    private int EntryIndex(long id)
    {
        for (int i = 0; i < _playbackController.Entries.Count; i++)
        {
            if (_playbackController.Entries[i].Id == id)
                return i;
        }
        return -1;
    }

    /// <summary>Selects an occurrence or an anchored range in the current visual order.</summary>
    private void OnEntrySelected(long entryId)
    {
        int index = EntryIndex(entryId);
        if (index < 0)
            return;
        if (Input.IsActionPressed("MultipleSelection"))
        {
            _selectionAnchorId ??= _playbackController.Entries.FirstOrDefault(e => _selectedEntries.Contains(e.Id))?.Id ?? entryId;
            int anchor = EntryIndex(_selectionAnchorId.Value);
            if (anchor < 0)
            {
                _selectionAnchorId = entryId;
                anchor = index;
            }
            _selectedEntries.Clear();
            for (int i = Math.Min(anchor, index); i <= Math.Max(anchor, index); i++)
                _selectedEntries.Add(_playbackController.Entries[i].Id);
        }
        else
        {
            _selectedEntries.Clear();
            _selectedEntries.Add(entryId);
            _selectionAnchorId = entryId;
        }
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        foreach (PlaylistTrackEntry row in _trackEntryContainer.GetChildren().OfType<PlaylistTrackEntry>())
            row.IsSelected = _selectedEntries.Contains(row.EntryId);
    }

    private void OnEntryActivated(long entryId) => EmitSignal(SignalName.EntryActivated, entryId);

    /// <summary>Forwards a drag request using occurrence identities and retains the moved selection.</summary>
    private void OnMoveRequested(long[] entryIds, long targetId, bool insertAfter)
    {
        if (EntryIndex(targetId) < 0 || entryIds.Contains(targetId))
            return;
        long[] surviving = [.. _playbackController.Entries.Where(e => entryIds.Contains(e.Id)).Select(e => e.Id)];
        if (surviving.Length == 0)
            return;
        _selectedEntries.Clear();
        _selectedEntries.UnionWith(surviving);
        _selectionAnchorId = surviving[0];
        EmitSignal(SignalName.MoveEntriesRequested, surviving, targetId, insertAfter);
        RefreshSelection();
    }

    private void OnRemoveSelectionRequested() => EmitSignal(SignalName.RemoveEntriesRequested, _selectedEntries.ToArray());
    private void OnCropRequested() => EmitSignal(SignalName.RetainEntriesRequested, _selectedEntries.ToArray());
    private void OnClearRequested() => EmitSignal(SignalName.ClearRequested);
    private void OnFilesRequested() => EmitSignal(SignalName.FilesRequested, false);
    private void OnFolderRequested() => EmitSignal(SignalName.FolderRequested, false);
    private void OnLoadPlaylistRequested() => EmitSignal(SignalName.LoadPlaylistRequested);
    private void OnSavePlaylistRequested() => EmitSignal(SignalName.SavePlaylistRequested);

    private void OnAddButtonPressed() => AddButtonDropdown.Activate(AddButton.GetGlobalRect());
    private void OnRemoveButtonPressed() => RemoveButtonDropdown.Activate(RemoveButton.GetGlobalRect());
    private void OnSelectButtonPressed() => SelectButtonDropdown.Activate(SelectButton.GetGlobalRect());
    private void OnMiscButtonPressed() => MiscButtonDropdown.Activate(MiscButton.GetGlobalRect());
    private void OnListOptionsButtonPressed() => ListOptionsButtonDropdown.Activate(ListOptionsButton.GetGlobalRect());

    private void OnInverseSelectionRequested()
    {
        foreach (QueueEntry entry in _playbackController.Entries)
        {
            if (!_selectedEntries.Remove(entry.Id))
                _selectedEntries.Add(entry.Id);
        }
        RefreshSelection();
    }

    private void OnSelectZeroRequested()
    {
        _selectedEntries.Clear();
        _selectionAnchorId = null;
        RefreshSelection();
    }

    private void OnSelectAllRequested()
    {
        _selectedEntries.UnionWith(_playbackController.Entries.Select(e => e.Id));
        _selectionAnchorId = _playbackController.Entries.Count > 0 ? _playbackController.Entries[0].Id : null;
        RefreshSelection();
    }

    /// <summary>Applies the playlist background and refreshes the shared scrollbar artwork.</summary>
    private void OnSkinChanged()
    {
        _scrollContainer.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = SkinLoader.Instance.PlaylistStyle.Background
        });
        _scrollContainer.GetVScrollBar()?.QueueRedraw();
    }
}
