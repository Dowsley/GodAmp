using GodAmp.Autoload;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.Equalizer;

/// <summary>Draws the classic interpolated band curve and preamp line using EQMAIN artwork.</summary>
public partial class EqualizerGraph : Control
{
    private const int GraphHeight = 19;
    private const int CurveWidth = 109;
    private const float BandSpacing = 12;
    private const float GainRangeDb = 24;
    private const float SplineBias = 0.1f;
    private AudioEffectEQ10 _equalizer = null!;
    private AudioEffectAmplify _preamp = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        int bus = AudioServer.GetBusIndex("Master");
        _equalizer = (AudioEffectEQ10)AudioServer.GetBusEffect(bus, AudioUtils.Eq10AudioEffectIndex);
        _preamp = (AudioEffectAmplify)AudioServer.GetBusEffect(bus, AudioUtils.AmplifyAudioEffectIndex);
        SignalBus.Instance.SkinChanged += QueueRedraw;
    }

    /// <inheritdoc />
    public override void _ExitTree() => SignalBus.Instance.SkinChanged -= QueueRedraw;

    /// <summary>Maps band gain to the classic graph coordinate, extending endpoint bands for interpolation.</summary>
    /// <param name="band">Band index; adjacent out-of-range indices repeat the nearest endpoint.</param>
    /// <returns>Unclipped vertical graph coordinate.</returns>
    private float BandPosition(int band) => (0.5f - _equalizer.GetBandGainDb(Mathf.Clamp(band, 0, _equalizer.GetBandCount() - 1)) / GainRangeDb) * GraphHeight;

    /// <inheritdoc />
    public override void _Draw()
    {
        if (_equalizer == null)
            return;
        Texture2D sheet = SkinLoader.Instance.GetSheet("EQMAIN");
        DrawTextureRectRegion(sheet, new Rect2(0, 0, 113, GraphHeight), new Rect2(0, 294, 113, GraphHeight));
        int preampY = Mathf.Clamp(GraphHeight - 1 - (int)((0.5f + _preamp.VolumeDb / GainRangeDb) * GraphHeight), 0, GraphHeight - 1);
        DrawTextureRectRegion(sheet, new Rect2(0, preampY, 113, 1), new Rect2(0, 314, 113, 1));
        int previous = -1;
        for (int x = 0; x < CurveWidth; x++)
        {
            float frame = x / BandSpacing;
            int band = (int)frame;
            float t = frame - band;
            float before = BandPosition(band - 1), start = BandPosition(band);
            float end = BandPosition(band + 1), after = BandPosition(band + 2);
            float outgoing = ((1 + SplineBias) * (start - before) + (1 - SplineBias) * (end - start)) / 2;
            float incoming = ((1 + SplineBias) * (end - start) + (1 - SplineBias) * (after - end)) / 2;
            float point = (2 * t * t * t - 3 * t * t + 1) * start + (t * t * t - 2 * t * t + t) * outgoing
                + (-2 * t * t * t + 3 * t * t) * end + (t * t * t - t * t) * incoming;
            int y = Mathf.Clamp((int)point, 0, GraphHeight - 1);
            int top = previous < 0 ? y : Mathf.Min(y, previous);
            int height = previous < 0 ? 1 : Mathf.Abs(y - previous) + 1;
            DrawTextureRectRegion(sheet, new Rect2(x + 2, top, 1, height), new Rect2(115, 294 + top, 1, height));
            previous = y;
        }
    }
}
