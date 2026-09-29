using System;
using System.Collections.Generic;
using System.Linq;
using GodAmp.Autoload;
using GodAmp.Components;
using GodAmp.Audio.Playback;
using GodAmp.Data;
using GodAmp.Utils;
using GodAmp.Controls.Playlist.FileInfo;
using GodAmp.Controls.Playlist.Search;
using Godot;

namespace GodAmp.Controls.Playlist;

/// <summary>Displays the shared queue and owns selection independently of playback.</summary>
public partial class Playlist : WindowPanelContainer
{
    [Signal] public delegate void EntryActivatedEventHandler(long entryId);
    [Signal] public delegate void RemoveEntriesRequestedEventHandler(long[] entryIds);
    [Signal] public delegate void OrderRequestedEventHandler(PlaylistOrder order);
    [Signal] public delegate void RemoveMissingRequestedEventHandler();
    [Signal] public delegate void RetainEntriesRequestedEventHandler(long[] entryIds);
    [Signal] public delegate void ClearRequestedEventHandler();
    [Signal] public delegate void FilesRequestedEventHandler(bool replace);
    [Signal] public delegate void FolderRequestedEventHandler(bool replace);
    [Signal] public delegate void LoadPlaylistRequestedEventHandler();
    [Signal] public delegate void SavePlaylistRequestedEventHandler();
    [Signal] public delegate void MoveEntriesRequestedEventHandler(long[] entryIds, long targetId, bool insertAfter);
    [Signal] public delegate void MetadataRefreshRequestedEventHandler(long[] entryIds);
    [Signal] public delegate void PlayRequestedEventHandler();
    [Signal] public delegate void PauseRequestedEventHandler();
    [Signal] public delegate void StopRequestedEventHandler();
    [Signal] public delegate void PreviousRequestedEventHandler();
    [Signal] public delegate void NextRequestedEventHandler();

    [ExportGroup("Config")]
    [Export] public PackedScene TrackLabelScene = null!;

    [ExportGroup("References")]
    [Export] private PlaybackController _playbackController = null!;
    [ExportSubgroup("Controls")]
    [Export] private VBoxContainer _trackEntryContainer = null!;
    [Export] private ScrollContainer _scrollContainer = null!;
    [Export] private Label _windowshadeTitle = null!;
    [Export] private Label _windowshadeDuration = null!;
    [Export] private PlaylistFooter _footer = null!;
    [Export] private FileInfoDialog _fileInfo = null!;
    [Export] private PopupMenu _contextMenu = null!;
    [Export] private JumpToTrackDialog _search = null!;
    [Export] private Shortcut _jumpShortcut = null!;

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
    private readonly HashSet<long> _presentedSelection = [];
    private readonly Dictionary<long, PlaylistTrackEntry> _rows = [];
    private long? _selectionAnchorId;
    private long? _currentRowId;
    private long? _focusedEntryId;
    private long? _inspectedEntryId;

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

    /// <summary>Reconciles queue order while retaining surviving rows, selection anchors, and scroll state.</summary>
    public void Refresh()
    {
        int focusedIndex = _focusedEntryId is { } focused && _rows.TryGetValue(focused, out var focusedRow) ? focusedRow.GetIndex() : 0;
        bool restoreFocus = _focusedEntryId is { } focusedId && _rows.TryGetValue(focusedId, out var activeRow) && activeRow.HasFocus();
        HashSet<long> surviving = [.. _playbackController.Entries.Select(e => e.Id)];
        _selectedEntries.IntersectWith(surviving);
        _presentedSelection.IntersectWith(surviving);
        if (_selectionAnchorId is { } anchor && EntryIndex(anchor) < 0)
            _selectionAnchorId = null;
        foreach (long id in _rows.Keys.Where(id => !surviving.Contains(id)).ToArray())
        {
            PlaylistTrackEntry row = _rows[id];
            DisconnectRow(row);
            _rows.Remove(id);
            _trackEntryContainer.RemoveChild(row);
            row.QueueFree();
        }

        for (int i = 0; i < _playbackController.Entries.Count; i++)
        {
            QueueEntry entry = _playbackController.Entries[i];
            string title = AudioUtils.GetFullTrackTitle(entry.Track, i + 1);
            if (!_rows.TryGetValue(entry.Id, out PlaylistTrackEntry? row))
            {
                row = TrackLabelScene.Instantiate<PlaylistTrackEntry>();
                _rows.Add(entry.Id, row);
                _trackEntryContainer.AddChild(row);
                row.SelectionRequested += SelectEntry;
                row.ContextRequested += OnContextRequested;
                row.KeyboardRequested += OnKeyboardRequested;
                row.Activated += OnEntryActivated;
                row.MoveRequested += OnMoveRequested;
                row.Setup(title, entry.Track.Duration, entry.Id, false);
            }
            else
                row.UpdateMetadata(title, entry.Track.Duration);
            if (row.GetIndex() != i)
                _trackEntryContainer.MoveChild(row, i);
        }
        RefreshSelection();
        RefreshCurrentEntry();
        if (_focusedEntryId is { } previousFocus && !surviving.Contains(previousFocus))
        {
            _focusedEntryId = _playbackController.Entries.Count > 0
                ? _playbackController.Entries[Math.Min(focusedIndex, _playbackController.Entries.Count - 1)].Id : null;
            if (restoreFocus && _focusedEntryId is { } nextFocus)
                FocusEntry(nextFocus);
        }
        RefreshFileInfo();
        if (_search.Visible)
            _search.Refresh();
    }

    /// <summary>Updates the previous and current occurrence without changing UI selection.</summary>
    public void RefreshCurrentEntry()
    {
        QueueEntry? current = _playbackController.CurrentEntry;
        _windowshadeTitle.Text = current == null ? "" : AudioUtils.GetFullTrackTitle(current.Track, _playbackController.CurrentIndex + 1);
        _windowshadeDuration.Text = current == null ? "" : TimeUtils.FormatAsTrackTime(current.Track.Duration);
        if (_currentRowId == current?.Id)
            return;
        if (_currentRowId is { } previous && _rows.TryGetValue(previous, out PlaylistTrackEntry? previousRow))
            previousRow.SetCurrent(false);
        _currentRowId = current?.Id;
        if (_currentRowId is { } id && _rows.TryGetValue(id, out PlaylistTrackEntry? currentRow))
            currentRow.SetCurrent(true);
    }

    /// <summary>Refreshes metadata for surviving occurrences without changing row identity or selection.</summary>
    /// <param name="entryIds">Occurrences whose track metadata changed.</param>
    public void RefreshMetadata(long[] entryIds)
    {
        foreach (long id in entryIds)
        {
            int index = EntryIndex(id);
            if (index >= 0 && _rows.TryGetValue(id, out PlaylistTrackEntry? row))
            {
                Track track = _playbackController.Entries[index].Track;
                row.UpdateMetadata(AudioUtils.GetFullTrackTitle(track, index + 1), track.Duration);
            }
        }
        if (_currentRowId is { } current && entryIds.Contains(current))
            RefreshCurrentEntry();
        RefreshDuration();
        RefreshFileInfo();
        if (_search.Visible)
            _search.Refresh();
    }

    /// <summary>Disconnects subscriptions owned by dynamically instantiated rows.</summary>
    private void DisconnectRows()
    {
        foreach (PlaylistTrackEntry row in _rows.Values)
            DisconnectRow(row);
    }

    private void DisconnectRow(PlaylistTrackEntry row)
    {
        row.SelectionRequested -= SelectEntry;
        row.ContextRequested -= OnContextRequested;
        row.KeyboardRequested -= OnKeyboardRequested;
        row.Activated -= OnEntryActivated;
        row.MoveRequested -= OnMoveRequested;
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

    /// <summary>Applies range or additive selection using stable occurrence identities.</summary>
    /// <param name="entryId">Occurrence receiving selection.</param>
    /// <param name="range">Whether to extend from the selection anchor.</param>
    /// <param name="toggle">Whether to toggle an occurrence or add a range to the existing selection.</param>
    private void SelectEntry(long entryId, bool range, bool toggle)
    {
        int index = EntryIndex(entryId);
        if (index < 0)
            return;
        _focusedEntryId = entryId;
        if (range)
        {
            _selectionAnchorId ??= _playbackController.Entries.FirstOrDefault(e => _selectedEntries.Contains(e.Id))?.Id ?? entryId;
            int anchor = EntryIndex(_selectionAnchorId.Value);
            if (anchor < 0)
            {
                _selectionAnchorId = entryId;
                anchor = index;
            }
            if (!toggle)
                _selectedEntries.Clear();
            for (int i = Math.Min(anchor, index); i <= Math.Max(anchor, index); i++)
                _selectedEntries.Add(_playbackController.Entries[i].Id);
        }
        else if (toggle)
        {
            if (!_selectedEntries.Remove(entryId))
                _selectedEntries.Add(entryId);
            _selectionAnchorId = entryId;
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
        HashSet<long> changed = [.. _presentedSelection];
        changed.SymmetricExceptWith(_selectedEntries);
        foreach (long id in changed)
            if (_rows.TryGetValue(id, out PlaylistTrackEntry? row))
                row.IsSelected = _selectedEntries.Contains(id);
        _presentedSelection.Clear();
        _presentedSelection.UnionWith(_selectedEntries);
        RefreshDuration();
    }

    private void RefreshDuration() => _footer.UpdateDuration(
        _playbackController.Entries.Where(entry => _selectedEntries.Contains(entry.Id)).Sum(entry => entry.Track.Duration),
        _playbackController.Entries.Sum(entry => entry.Track.Duration));

    private void RefreshFileInfo()
    {
        QueueEntry? entry = _playbackController.Entries.FirstOrDefault(entry => entry.Id == _inspectedEntryId);
        if (entry == null)
        {
            _fileInfo.Hide();
            _inspectedEntryId = null;
        }
        else
            _fileInfo.Display(entry.Track);
    }

    private long? SelectedEntryId() => _focusedEntryId is { } focused && _selectedEntries.Contains(focused)
        ? focused : _playbackController.Entries.FirstOrDefault(entry => _selectedEntries.Contains(entry.Id))?.Id;

    private void OnFileInfoRequested()
    {
        _inspectedEntryId = SelectedEntryId();
        RefreshFileInfo();
        if (_inspectedEntryId != null)
            _fileInfo.PopupCentered();
    }

    private void OnRefreshMetadataRequested() => EmitSignal(SignalName.MetadataRefreshRequested, _selectedEntries.ToArray());

    private void OnContextRequested(long entryId)
    {
        Viewport viewport = GetViewport();
        ShowContextMenu(entryId, viewport.GetFinalTransform() * viewport.GetMousePosition());
    }

    private void ShowContextMenu(long entryId, Vector2 position)
    {
        if (!_selectedEntries.Contains(entryId))
            SelectEntry(entryId, false, false);
        _focusedEntryId = entryId;
        _contextMenu.PopupOnParent(new Rect2I((Vector2I)position, Vector2I.Zero));
    }

    private void OnContextCommand(long id)
    {
        switch ((PlaylistCommand)id)
        {
            case PlaylistCommand.Play:
                if (SelectedEntryId() is { } selected) OnEntryActivated(selected);
                break;
            case PlaylistCommand.FileInfo: OnFileInfoRequested(); break;
            case PlaylistCommand.RefreshMetadata: OnRefreshMetadataRequested(); break;
            case PlaylistCommand.Remove: OnRemoveSelectionRequested(); break;
            case PlaylistCommand.Crop: OnCropRequested(); break;
            case PlaylistCommand.SelectAll: OnSelectAllRequested(); break;
            case PlaylistCommand.JumpToTrack: OpenSearch(); break;
        }
    }

    private void FocusEntry(long id)
    {
        _focusedEntryId = id;
        if (_rows.TryGetValue(id, out var row))
        {
            row.GrabFocus();
            _scrollContainer.EnsureControlVisible(row);
        }
    }

    /// <summary>Opens a search over the shared queue without changing its order or selection.</summary>
    public void OpenSearch() => _search.Open(_playbackController.Entries);

    /// <summary>Handles the scene-configured search shortcut after focused controls have processed input.</summary>
    /// <param name="input">An unhandled event from a player window.</param>
    /// <returns>Whether the search dialog was opened.</returns>
    public bool HandleJumpShortcut(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } || !_jumpShortcut.MatchesEvent(input))
            return false;
        OpenSearch();
        return true;
    }

    /// <inheritdoc />
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (HandleJumpShortcut(@event))
            GetViewport().SetInputAsHandled();
    }

    /// <summary>Handles row-local keyboard commands without intercepting dialogs or other windows.</summary>
    private void OnKeyboardRequested(long entryId, InputEventKey key)
    {
        int index = EntryIndex(entryId);
        if (index < 0)
            return;
        bool command = key.IsCommandOrControlPressed();
        int page = Math.Max(1, (int)(_scrollContainer.Size.Y / Math.Max(1, _rows[entryId].Size.Y)));
        int destination = key.Keycode switch
        {
            Key.Up => index - 1, Key.Down => index + 1,
            Key.Home => 0, Key.End => _playbackController.Entries.Count - 1,
            Key.Pageup => index - page, Key.Pagedown => index + page,
            _ => index
        };
        if (key.Keycode is Key.Up or Key.Down or Key.Home or Key.End or Key.Pageup or Key.Pagedown)
        {
            long target = _playbackController.Entries[Math.Clamp(destination, 0, _playbackController.Entries.Count - 1)].Id;
            if (!command || key.ShiftPressed)
                SelectEntry(target, key.ShiftPressed, command);
            FocusEntry(target);
            return;
        }
        _focusedEntryId = entryId;
        switch (key.Keycode)
        {
            case Key.Enter: case Key.KpEnter: OnEntryActivated(entryId); break;
            case Key.Space: SelectEntry(entryId, key.ShiftPressed, command); break;
            case Key.A when command: OnSelectAllRequested(); break;
            case Key.I when command: OnInverseSelectionRequested(); break;
            case Key.Delete when command && key.ShiftPressed: OnClearRequested(); break;
            case Key.Delete when command: OnCropRequested(); break;
            case Key.Delete: OnRemoveSelectionRequested(); break;
            case Key.Key3 when key.AltPressed: OnFileInfoRequested(); break;
            case Key.F5: OnRefreshMetadataRequested(); break;
            case Key.Menu: case Key.F10 when key.ShiftPressed:
                ShowContextMenu(entryId, GetViewport().GetFinalTransform() * _rows[entryId].GetGlobalTransformWithCanvas().Origin);
                break;
        }
    }

    private void OnPlayRequested() => EmitSignal(SignalName.PlayRequested);
    private void OnPauseRequested() => EmitSignal(SignalName.PauseRequested);
    private void OnStopRequested() => EmitSignal(SignalName.StopRequested);
    private void OnPreviousRequested() => EmitSignal(SignalName.PreviousRequested);
    private void OnNextRequested() => EmitSignal(SignalName.NextRequested);

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
    private void OnOrderRequested(PlaylistOrder order) => EmitSignal(SignalName.OrderRequested, (int)order);
    private void OnRemoveMissingRequested() => EmitSignal(SignalName.RemoveMissingRequested);
    private void OnCropRequested() => EmitSignal(SignalName.RetainEntriesRequested, _selectedEntries.ToArray());
    private void OnClearRequested() => EmitSignal(SignalName.ClearRequested);
    private void OnFilesRequested() => EmitSignal(SignalName.FilesRequested, false);
    private void OnOpenFilesRequested() => EmitSignal(SignalName.FilesRequested, true);
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
