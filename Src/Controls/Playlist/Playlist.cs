using System.Collections.Generic;
using System.Linq;
using GodAmp.Components;
using GodAmp.Data;
using GodAmp.Player;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.Playlist;

public partial class Playlist : WindowPanelContainer
{
	[Export] public PackedScene TrackLabelScene;
	
	[ExportCategory("Button dropdown refs")]
	[Export] public ButtonDropdown AddButtonDropdown;
	[Export] public ButtonDropdown RemoveButtonDropdown;
	[Export] public ButtonDropdown SelectButtonDropdown;
	[Export] public ButtonDropdown MiscButtonDropdown;
	[Export] public ButtonDropdown ListOptionsButtonDropdown;
	
	[ExportCategory("Button refs")]
	[Export] public TextureButton AddButton;
	[Export] public TextureButton RemoveButton;
	[Export] public TextureButton SelectButton;
	[Export] public TextureButton MiscButton;
	[Export] public TextureButton ListOptionsButton;
	
	private VBoxContainer _trackEntryContainer;
	private ScrollContainer _scrollContainer;

	private HashSet<Track> _selectedTracks = [];
	private List<Track> _playlistRef;
	private TrackPlayer _trackPlayerRef;
	private int? _selectionAnchorIndex;

	public override void _Ready()
	{
		_trackEntryContainer = GetNode<VBoxContainer>("%PlaylistTrackEntryContainer");
		_scrollContainer = GetNode<ScrollContainer>("%ScrollContainer");
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
}