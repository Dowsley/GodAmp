using GodAmp.Autoload;
using GodAmp.Audio.Playback;
using Godot;

namespace GodAmp.Controls.MasterPanel;

/// <summary>Draws classic transport and source-channel indicators from the active skin.</summary>
public partial class PlaybackIndicators : Control
{
    /// <summary>Scene-assigned owner supplying transport and source-channel information.</summary>
    [Export] public PlaybackController? Playback { get; set; }
    private int _transport = -1;
    private int _channels = -1;

    /// <inheritdoc />
    public override void _Ready()
    {
        SignalBus.Instance.SkinChanged += QueueRedraw;
        Refresh();
    }

    /// <inheritdoc />
    public override void _ExitTree() => SignalBus.Instance.SkinChanged -= QueueRedraw;

    /// <summary>Refreshes transport artwork when the selected occurrence or playback state changes.</summary>
    public void Refresh()
    {
        int transport = Playback?.CurrentEntry == null ? 27 : Playback.State switch
        {
            PlaybackState.Playing => 0,
            PlaybackState.Paused => 9,
            _ => 18
        };
        int channels = Playback?.CurrentEntry?.Track.Channels ?? 0;
        if (transport == _transport && channels == _channels)
            return;
        _transport = transport;
        _channels = channels;
        QueueRedraw();
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        if (_transport < 0)
            return;
        Texture2D playback = SkinLoader.Instance.GetSheet("PLAYPAUS");
        Texture2D channels = SkinLoader.Instance.GetSheet("MONOSTER");
        /* Source and destination rectangles are the classic Winamp bitmap format. */
        DrawTextureRectRegion(playback, new Rect2(26, 28, 9, 9), new Rect2(_transport, 0, 9, 9));
        bool playing = _transport == 0;
        DrawTextureRectRegion(playback, new Rect2(24, 28, playing ? 3 : 2, 9),
            new Rect2(playing ? 36 : 27, 0, playing ? 3 : 2, 9));
        DrawTextureRectRegion(channels, new Rect2(212, 41, 28, 12), new Rect2(29, _channels == 1 ? 0 : 12, 28, 12));
        DrawTextureRectRegion(channels, new Rect2(239, 41, 29, 12), new Rect2(0, _channels >= 2 ? 0 : 12, 29, 12));
    }
}
