using Godot;

namespace GodAmp.Presentation.Visualizer;

/// <summary>Supplies shared spectrum sampling and animation state to scene-based rendering strategies.</summary>
public abstract partial class VisualizerStrategy : Node2D
{
    private const float MinimumFrequency = 20f;
    private const float MaximumFrequency = 22050f;
    private const float BassUpperFrequency = 200f;
    private const float TrebleLowerFrequency = 2000f;
    private const float TrebleUpperFrequency = 20000f;
    private const float MinimumReactiveMagnitude = 0.001f;
    [ExportGroup("General")]
    [Export] public int UpdateEveryNFrames = 1;

    [ExportGroup("Warp")]
    [ExportSubgroup("Base")]
    [Export] public float FixedRotationValue = 0.05f;
    [Export] public float RotationSpeedFactor = 0.5f;
    [Export] public float RotationSpeed = 1.0f;
    [Export] public float TunnelDepth = 1.0f;
    [Export] public float Distortion = 1.0f;
    [Export] public float DecayRate = 0.65f;
    [Export] public float TrailIntensity = 0.8f;
    [Export] public Color LineColor = Colors.Cyan;
    [Export] public float GlowIntensity = 1.0f;
    [Export] public float FeedbackStrength = 0.4f;
    [Export] public float ColorDecay = 0.75f;
    [Export] public float ColorChangeSpeed = 0.1f;
    [Export] public float RandomOffsetAmount = 80.0f;

    [ExportSubgroup("Music Reactivity")]
    [Export] public float SmoothingFactor = 0.1f;
    [Export] public float DirectionSmoothingFactor = 0.05f;
    [Export] public float DirectionSensitivity = 2.0f;
    [Export] public float DepthSmoothingFactor = 0.05f;
    [Export] public float DepthSensitivity = 0.3f;

    public float SmoothedMagnitude = 0.0f;
    public float SmoothedDirection = 0.0f;
    public float SmoothedDepth = 0.0f;
    public float ColorHue = 0.0f;
    public Color FinalColor = Colors.Cyan;
    public float TimeOffset = 0.0f;

    protected AudioEffectSpectrumAnalyzerInstance Spectrum = null!;
    protected int FrameCount = 0;
    protected Vector2 ViewportSize;

    /// <summary>Initializes a dynamically instantiated strategy with its scene's shared audio analysis source.</summary>
    /// <param name="viewportSize">Current rendering dimensions.</param>
    /// <param name="spectrum">Analyzer owned and validated by the scene's audio controller.</param>
    public virtual void Initialize(Vector2 viewportSize, AudioEffectSpectrumAnalyzerInstance spectrum)
    {
        ViewportSize = viewportSize;
        Spectrum = spectrum;
    }

    /// <summary>Advances strategy-specific rendering for one animation frame.</summary>
    /// <param name="delta">Elapsed seconds since the previous frame.</param>
    public virtual void Update(double delta) { }

    /// <summary>Advances strategies that use the physics simulation on a fixed engine tick.</summary>
    /// <param name="delta">Elapsed seconds since the previous physics tick.</param>
    public virtual void PhysicsUpdate(double delta) { }

    /// <summary>Maps a sample boundary onto the visualization's logarithmic frequency range.</summary>
    /// <param name="index">Boundary index from zero through sampleCount.</param>
    /// <param name="sampleCount">Positive number of frequency bands.</param>
    /// <returns>Boundary frequency in hertz.</returns>
    protected static float GetFrequencyForSampleIndex(int index, int sampleCount)
        => Mathf.Exp(Mathf.Lerp(Mathf.Log(MinimumFrequency), Mathf.Log(MaximumFrequency), (float)index / sampleCount));

    /// <summary>Reads the mean stereo magnitude for a frequency band.</summary>
    /// <param name="minHz">Lower frequency in hertz.</param>
    /// <param name="maxHz">Upper frequency in hertz.</param>
    /// <returns>Mean of the analyzer's left and right channel magnitudes.</returns>
    protected float GetFrequencyRangeMagnitude(float minHz, float maxHz)
    {
        Vector2 magnitude = Spectrum.GetMagnitudeForFrequencyRange(minHz, maxHz);
        return (magnitude.X + magnitude.Y) * 0.5f;
    }

    /// <summary>Updates shared magnitude, direction, depth and hue from the audio owner's analyzer.</summary>
    /// <param name="delta">Elapsed seconds used for hue animation.</param>
    /// <param name="sampleCount">Positive number of logarithmic bands to average.</param>
    /// <param name="magnitudeResponse">Multiplier for this strategy's magnitude smoothing.</param>
    protected void UpdateAudioReactivity(double delta, int sampleCount, float magnitudeResponse = 1f)
    {
        float sum = 0f;
        for (int i = 0; i < sampleCount; i++)
            sum += GetFrequencyRangeMagnitude(GetFrequencyForSampleIndex(i, sampleCount), GetFrequencyForSampleIndex(i + 1, sampleCount));
        SmoothedMagnitude = Mathf.Lerp(SmoothedMagnitude, sum / sampleCount, SmoothingFactor * magnitudeResponse);

        float lowMagnitude = GetFrequencyRangeMagnitude(MinimumFrequency, BassUpperFrequency);
        float highMagnitude = GetFrequencyRangeMagnitude(TrebleLowerFrequency, TrebleUpperFrequency);
        float totalMagnitude = lowMagnitude + highMagnitude;
        if (totalMagnitude > MinimumReactiveMagnitude)
        {
            float balance = (highMagnitude - lowMagnitude) / totalMagnitude;
            SmoothedDirection = Mathf.Lerp(SmoothedDirection, Mathf.Tanh(balance * DirectionSensitivity), DirectionSmoothingFactor);
            SmoothedDepth = Mathf.Lerp(SmoothedDepth, Mathf.Tanh(totalMagnitude * DepthSensitivity), DepthSmoothingFactor);
        }
        ColorHue = (ColorHue + ColorChangeSpeed * (float)delta) % 1.0f;
    }
}
