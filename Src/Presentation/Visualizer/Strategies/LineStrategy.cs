using System.Collections.Generic;
using Godot;

namespace GodAmp.Presentation.Visualizer.Strategies;

public partial class LineStrategy : VisualizerStrategy
{
    [ExportGroup("Raw Waveform")]
    [Export] private float _amplitude = 2.0f;
    [Export] private float _noiseAmount = 0.07f;
    [Export] private int _sampleCount = 64;
    [Export] private int _pointsPerSegment = 5;
    [Export(PropertyHint.Range, "0.0,1.0")] private float _lineVerticalPosition = 0.75f;

    [Export] private Line2D _line = null!;
    private Vector2[] _points = null!;

    public override void Initialize(Vector2 viewportSize, AudioEffectSpectrumAnalyzerInstance spectrum)
    {
        base.Initialize(viewportSize, spectrum);
        InitializePoints();
        InitializeLines();
    }

    public override void Update(double delta)
    {
        FrameCount++;
        if (FrameCount % UpdateEveryNFrames != 0)
            return;

        _line.DefaultColor = FinalColor;
        UpdateAudioReactivity(delta, _sampleCount);
        TimeOffset = FixedRotationValue * (1.0f + SmoothedDirection * RotationSpeedFactor);
        UpdateWaveform();
    }

    private void InitializePoints()
    {
        _points = new Vector2[_sampleCount];
        ResetPoints();
    }

    private void ResetPoints()
    {
        float width = ViewportSize.X;
        float height = ViewportSize.Y;

        for (int i = 0; i < _sampleCount; i++)
        {
            _points[i] = new Vector2(
                width * i / (_sampleCount - 1),
                height * _lineVerticalPosition
            );
        }
    }

    private void InitializeLines()
    {
        _line.DefaultColor = LineColor;
        _line.Points = _points;
    }

    private void UpdateWaveform()
    {
        float baselineHeight = ViewportSize.Y * _lineVerticalPosition;

        float randomX = (float)GD.RandRange(-RandomOffsetAmount, RandomOffsetAmount);
        float randomY = (float)GD.RandRange(-RandomOffsetAmount, RandomOffsetAmount);

        for (int i = 0; i < _sampleCount; i++)
        {
            float hzMin = GetFrequencyForSampleIndex(i, _sampleCount);
            float hzMax = GetFrequencyForSampleIndex(i + 1, _sampleCount);

            float value = GetFrequencyRangeMagnitude(hzMin, hzMax);
            value *= _amplitude;
            value += GD.Randf() * _noiseAmount - _noiseAmount * 0.5f;
            _points[i] = new Vector2(
                ViewportSize.X * i / (_sampleCount - 1) + randomX,
                baselineHeight - value * baselineHeight + randomY
            );
        }

        var smoothPoints = GenerateCatmullRomPoints(_points, _pointsPerSegment).ToArray();
        _line.Points = smoothPoints;
    }

    private static List<Vector2> GenerateCatmullRomPoints(Vector2[] controlPoints, int pointsPerSegment = 5)
    {
        var smoothPoints = new List<Vector2>();

        for (int i = 0; i < controlPoints.Length - 1; i++)
        {
            Vector2 p0 = i > 0 ? controlPoints[i - 1] : controlPoints[i];
            Vector2 p1 = controlPoints[i];
            Vector2 p2 = controlPoints[i + 1];
            Vector2 p3 = i + 2 < controlPoints.Length ? controlPoints[i + 2] : controlPoints[i + 1];

            for (int j = 0; j < pointsPerSegment; j++)
            {
                float t = j / (float)pointsPerSegment;
                float t2 = t * t;
                float t3 = t2 * t;

                Vector2 interpolated = 0.5f * (
                    2f * p1 +
                    (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                );

                smoothPoints.Add(interpolated);
            }
        }

        smoothPoints.Add(controlPoints[^1]);
        return smoothPoints;
    }

}
