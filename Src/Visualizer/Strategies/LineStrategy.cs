using System.Collections.Generic;
using Godot;

namespace GodAmp.Visualizer.Strategies;

public partial class LineStrategy : VisualizerStrategy
{
    [ExportGroup("Raw Waveform")]
    [Export] public float Amplitude = 2.0f;
    [Export] public float NoiseAmount = 0.07f;
    [Export] public int SampleCount = 64;
    [Export] public int PointsPerSegment = 5;
        
    private Line2D _line;
    private AudioEffectSpectrumAnalyzerInstance _spectrum;
    private Vector2[] _points;

    public override void _Ready()
    {
        _line = GetNode<Line2D>("Line2D");
    }
    
    public override void Initialize(Vector2 viewportSize)
    {
        base.Initialize(viewportSize);
        InitializePoints();
        InitializeLines();
    }

    public override void Update(double delta)
    {
        FrameCount++;
        if (FrameCount % UpdateEveryNFrames != 0)
            return;

        _line.DefaultColor = FinalColor;
        UpdateAudioReactivity(delta);
        UpdateWaveform();
    }
    
    private void InitializePoints()
    {
        _points = new Vector2[SampleCount];
        ResetPoints();
    }
    
    private void ResetPoints()
    {
        float width = ViewportSize.X;
        float height = ViewportSize.Y;
            
        for (int i = 0; i < SampleCount; i++)
        {
            _points[i] = new Vector2(
                width * i / (SampleCount - 1),
                height / 2
            );
        }
    }
    
    private void InitializeLines()
    {
        _line.Width = 2.0f;
        _line.DefaultColor = LineColor;
        _line.JointMode = Line2D.LineJointMode.Round;
        _line.BeginCapMode = Line2D.LineCapMode.Round;
        _line.EndCapMode = Line2D.LineCapMode.Round;
        _line.Antialiased = true;
        _line.Points = _points;
    }
    
    private void UpdateWaveform()
    {
        float halfHeight = ViewportSize.Y / 2;
            
        float randomX = (float)(GD.RandRange(-RandomOffsetAmount, RandomOffsetAmount));
        float randomY = (float)(GD.RandRange(-RandomOffsetAmount, RandomOffsetAmount));

        for (int i = 0; i < SampleCount; i++)
        {
            float hzMin = GetFrequencyForSampleIndex(i);
            float hzMax = GetFrequencyForSampleIndex(i + 1);

            float value = GetFrequencyRangeMagnitude(hzMin, hzMax);
            value *= Amplitude;
            value += GD.Randf() * NoiseAmount - (NoiseAmount * 0.5f);
            _points[i] = new Vector2(
                ViewportSize.X * i / (SampleCount - 1) + randomX,
                halfHeight - (value * halfHeight) + randomY
            );
        }

        var smoothPoints = GenerateCatmullRomPoints(_points, PointsPerSegment).ToArray();
        _line.Points = smoothPoints;
    }
    
    private float GetFrequencyForSampleIndex(int index)
    {
        return Mathf.Exp(Mathf.Lerp(Mathf.Log(20f), Mathf.Log(22050f), (float)index / SampleCount));
    }
    
    private float GetAverageAudioMagnitude()
    {
        float sum = 0f;
        for (int i = 0; i < SampleCount; i++)
        {
            float hzMin = GetFrequencyForSampleIndex(i);
            float hzMax = GetFrequencyForSampleIndex(i + 1);
            sum += GetFrequencyRangeMagnitude(hzMin, hzMax);
        }
        return sum / SampleCount;
    }
    
    private float GetFrequencyRangeMagnitude(float minHz, float maxHz)
    {
        var magnitude = Spectrum.GetMagnitudeForFrequencyRange(minHz, maxHz);
        return (magnitude.X + magnitude.Y) * 0.5f;
    }
    
    private static List<Vector2> GenerateCatmullRomPoints(Vector2[] controlPoints, int pointsPerSegment = 5)
    {
        var smoothPoints = new List<Vector2>();

        for (int i = 0; i < controlPoints.Length - 1; i++)
        {
            Vector2 p0 = i > 0 ? controlPoints[i - 1] : controlPoints[i];
            Vector2 p1 = controlPoints[i];
            Vector2 p2 = controlPoints[i + 1];
            Vector2 p3 = (i + 2 < controlPoints.Length) ? controlPoints[i + 2] : controlPoints[i + 1];

            for (int j = 0; j < pointsPerSegment; j++)
            {
                float t = j / (float)pointsPerSegment;
                float t2 = t * t;
                float t3 = t2 * t;

                Vector2 interpolated = 0.5f * (
                    (2f * p1) +
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
    
    private void UpdateAudioReactivity(double delta)
    {
        float currentMagnitude = GetAverageAudioMagnitude();
        SmoothedMagnitude = Mathf.Lerp(SmoothedMagnitude, currentMagnitude, SmoothingFactor);
            
        float lowFreqMagnitude = GetFrequencyRangeMagnitude(20f, 200f);
        float highFreqMagnitude = GetFrequencyRangeMagnitude(2000f, 20000f);
        float totalMagnitude = lowFreqMagnitude + highFreqMagnitude;
            
        if (totalMagnitude > 0.001f)
        {
            float normalizedBalance = (highFreqMagnitude - lowFreqMagnitude) / totalMagnitude;
            float directionTarget = Mathf.Tanh(normalizedBalance * DirectionSensitivity);
            SmoothedDirection = Mathf.Lerp(SmoothedDirection, directionTarget, DirectionSmoothingFactor);
                
            float depthTarget = Mathf.Tanh(totalMagnitude * DepthSensitivity);
            SmoothedDepth = Mathf.Lerp(SmoothedDepth, depthTarget, DepthSmoothingFactor);
        }
            
        TimeOffset = FixedRotationValue * (1.0f + (SmoothedDirection * RotationSpeedFactor));
        ColorHue = (ColorHue + ColorChangeSpeed * (float)delta) % 1.0f;
    }
}