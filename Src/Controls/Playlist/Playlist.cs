using System.Collections.Generic;
using System.Linq;
using GodAmp.Autoload;
using GodAmp.Components;
using GodAmp.Data;
using GodAmp.Player;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.Playlist;

public partial class Playlist : WindowPanelContainer
{
	[ExportGroup("Config")]
	[Export] public PackedScene TrackLabelScene;
	
	[ExportGroup("References")]
	[ExportSubgroup("Controls")]
	[Export] private VBoxContainer _trackEntryContainer;
	[Export] private ScrollContainer _scrollContainer;
	
	[ExportSubgroup("Button dropdowns")]
	[Export] public ButtonDropdown AddButtonDropdown;
	[Export] public ButtonDropdown RemoveButtonDropdown;
	[Export] public ButtonDropdown SelectButtonDropdown;
	[Export] public ButtonDropdown MiscButtonDropdown;
	[Export] public ButtonDropdown ListOptionsButtonDropdown;
	
	[ExportSubgroup("Buttons")]
	[Export] public TextureButton AddButton;
	[Export] public TextureButton RemoveButton;
	[Export] public TextureButton SelectButton;
	[Export] public TextureButton MiscButton;
	[Export] public TextureButton ListOptionsButton;

	private HashSet<Track> _selectedTracks = [];
	private List<Track> _playlistRef;
	private TrackPlayer _trackPlayerRef;
	private int? _selectionAnchorIndex;

	public override void _Ready()
	{
		base._Ready();
		SignalBus.Instance.InverseSelectionRequested += OnInverseSelectionRequested;
		SignalBus.Instance.SelectZeroRequested += OnSelectZeroRequested;
		SignalBus.Instance.SelectAllRequested += OnSelectAllRequested;
		SignalBus.Instance.SkinChanged += OnSkinChanged;
	}

	public void Setup(TrackPlayer trackPlayerRef, List<Track> playlist)
	{
		_trackPlayerRef = trackPlayerRef;
		_playlistRef = playlist;
	}

	public void Refresh()
	{
		HashSet<Track> newSelectedTracks = [];
		
		_trackEntryContainer.GetChildren().ToList().ForEach(child => child.QueueFree());
		for (int i = 0; i < _playlistRef.Count; i++)
		{
			var track = _playlistRef[i];
			var label = TrackLabelScene.Instantiate<PlaylistTrackEntry>();
			_trackEntryContainer.AddChild(label);
			label.Selected += OnTrackSelected;
			var isSelected = _selectedTracks.Contains(track);
			label.Setup(AudioUtils.GetFullTrackTitle(
				track, i+1), 
				track.Duration,
				i,
				isSelected,
				track == _trackPlayerRef.CurrentTrack
			);
			
			if (isSelected)
				newSelectedTracks.Add(track);
		}

		_selectedTracks = newSelectedTracks;
	}

	public HashSet<int> GetSelectedIndices()
	{
		var indices = new HashSet<int>();
		foreach (var child in _trackEntryContainer.GetChildren())
		{
			var label = (PlaylistTrackEntry)child;
			if (label.IsSelected)
				indices.Add(label.Index);
		}
		return indices;
	}

	public HashSet<Track> GetSelectedTracks()
	{
		return [.._selectedTracks];
	}

	private void OnTrackSelected(int index)
	{
		if (Input.IsActionPressed("MultipleSelection"))
			MultipleSelection(index);
		else
			SingleSelection(index);
	}

	private void MultipleSelection(int index)
	{
		if (_selectionAnchorIndex is null)
		{
			int? topSelectedIndex = null;
			foreach (var child in _trackEntryContainer.GetChildren())
			{
				var labelScan = (PlaylistTrackEntry)child;
				if (labelScan.IsSelected)
				{
					if (topSelectedIndex is null || labelScan.Index < topSelectedIndex.Value)
						topSelectedIndex = labelScan.Index;
				}
			}
			_selectionAnchorIndex = topSelectedIndex ?? index;
		}

		int start = System.Math.Min(_selectionAnchorIndex.Value, index);
		int end = System.Math.Max(_selectionAnchorIndex.Value, index);

		_selectedTracks.Clear();
		foreach (var child in _trackEntryContainer.GetChildren())
		{
			var label = (PlaylistTrackEntry)child;
			bool inRange = label.Index >= start && label.Index <= end;
			label.IsSelected = inRange;
			if (inRange)
			{
				_selectedTracks.Add(_playlistRef[label.Index]);
			}
		}
	}

	private void SingleSelection(int index)
	{
		foreach (var child in _trackEntryContainer.GetChildren())
		{
			var label = (PlaylistTrackEntry)child;
			bool isCurrent = label.Index == index;
			label.IsSelected = isCurrent;
		}

		_selectedTracks.Clear();
		_selectedTracks.Add(_playlistRef[index]);
		_selectionAnchorIndex = index;
	}

	private void OnAddButtonPressed()
	{
		AddButtonDropdown.Activate(AddButton.GlobalPosition, AddButton.Size);
	}
	
	private void OnRemoveButtonPressed()
	{
		RemoveButtonDropdown.Activate(RemoveButton.GlobalPosition, RemoveButton.Size);
	}
	
	private void OnSelectButtonPressed()
	{
		SelectButtonDropdown.Activate(SelectButton.GlobalPosition, SelectButton.Size);
	}
	
	private void OnMiscButtonPressed()
	{
		MiscButtonDropdown.Activate(MiscButton.GlobalPosition, MiscButton.Size);
	}
	
	private void OnListOptionsButtonPressed()
	{
		ListOptionsButtonDropdown.Activate(
			ListOptionsButton.GlobalPosition, ListOptionsButton.Size);
	}

	private void OnInverseSelectionRequested()
	{
		_selectedTracks.Clear();
		foreach (var child in _trackEntryContainer.GetChildren())
		{
			var label = (PlaylistTrackEntry)child;
			label.IsSelected = !label.IsSelected;
			if (label.IsSelected)
				_selectedTracks.Add(_playlistRef[label.Index]);
		}
	}

	private void OnSelectZeroRequested()
	{
		_selectedTracks.Clear();
		foreach (var child in _trackEntryContainer.GetChildren())
		{
			var label = (PlaylistTrackEntry)child;
			label.IsSelected = false;
		}
		_selectionAnchorIndex = null;
	}

	private void OnSelectAllRequested()
	{
		_selectedTracks = new HashSet<Track>(_playlistRef);
		foreach (var child in _trackEntryContainer.GetChildren())
		{
			var label = (PlaylistTrackEntry)child;
			label.IsSelected = true;
		}
		_selectionAnchorIndex = 0;
	}

	private void OnSkinChanged()
	{
		var vScrollBar = _scrollContainer.GetVScrollBar();
		vScrollBar?.QueueRedraw();
	}

	public void ReorderSelectedTracks(List<int> selectedIndices, int targetIndex, bool insertAfter)
	{
		if (_playlistRef is null || selectedIndices is null || selectedIndices.Count == 0)
			return;

		// normalize and validate indices
		var normalized = selectedIndices
			.Distinct()
			.Where(i => i >= 0 && i < _playlistRef.Count)
			.OrderBy(i => i)
			.ToList();
		if (normalized.Count == 0)
			return;

		// no-op if dropping inside the selected contiguous block
		int minSel = normalized.First();
		int maxSel = normalized.Last();
		if ((!insertAfter && targetIndex >= minSel && targetIndex <= maxSel) ||
			(insertAfter && targetIndex >= minSel && targetIndex < maxSel))
			return;

		int baseInsertIndex = insertAfter ? targetIndex + 1 : targetIndex;
		if (baseInsertIndex < 0) baseInsertIndex = 0;
		if (baseInsertIndex > _playlistRef.Count) baseInsertIndex = _playlistRef.Count;

		int removedBefore = normalized.Count(i => i < baseInsertIndex);
		int insertIndex = baseInsertIndex - removedBefore;
		if (insertIndex < 0) insertIndex = 0;
		if (insertIndex > _playlistRef.Count - normalized.Count)
			insertIndex = _playlistRef.Count - normalized.Count;

		var movedTracks = new List<Track>(normalized.Count);
		movedTracks.AddRange(normalized.Select(idx => _playlistRef[idx]));
		for (int k = normalized.Count - 1; k >= 0; k--) // remove from end to start to avoid shifting
			_playlistRef.RemoveAt(normalized[k]);

		_playlistRef.InsertRange(insertIndex, movedTracks);
		_selectedTracks = new HashSet<Track>(movedTracks); // preserve selection of moved tracks
		_selectionAnchorIndex = insertIndex;

		Refresh();
	}
}