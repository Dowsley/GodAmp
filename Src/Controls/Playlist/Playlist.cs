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
	
	private VBoxContainer _trackEntryContainer;
	private ScrollContainer _scrollContainer;

	private List<Track> _playlistRef;
	private TrackPlayer _trackPlayerRef;

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
		_trackEntryContainer.GetChildren().ToList().ForEach(child => child.QueueFree());
		var i = 0;
		foreach (var track in _playlistRef)
		{
			var label = TrackLabelScene.Instantiate<PlaylistTrackEntry>();
			_trackEntryContainer.AddChild(label);
			label.Setup(AudioUtils.GetFullTrackTitle(track), track.Duration, i, track == _trackPlayerRef.CurrentTrack);
			i += 1;
		}
	}
}