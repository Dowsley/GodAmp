using Godot;

namespace GodAmp.Visualizer;

public abstract partial class VisualizerStrategy : Node2D
{
    [ExportGroup("General")]
    [Export] public int UpdateEveryNFrames = 1;

    [ExportGroup("Warp")]
    [ExportSubgroup("Base")]
    [Export] public float FixedRotationValue = 0.05f;
    [Export] public float RotationSpeedFactor = 0.5f; // How much the song affects rotation speed
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
    [Export] public float SmoothingFactor = 0.1f; // higher=responsive size changes
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

    public virtual void Update(double delta)
    {
    }

}
