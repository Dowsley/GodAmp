using Godot;

namespace GodAmp.Presentation.Visualizer.Strategies;

/// <summary>Drives the authored squares and viewport boundaries from spectrum samples on physics ticks.</summary>
public partial class SquareStrategy : VisualizerStrategy
{
    private const int SpectrumSampleCount = 64;
    private const float MagnitudeResponse = 2f;
    private const float BoundaryThickness = 50f;
    private const float ResetMargin = 100f;
    private const float SizeResponsePerSecond = 21.4f;
    private const float MinimumFrequency = 20f;
    private const float MaximumFrequency = 20000f;
    private const float DirectionJitter = 0.5f;
    private static readonly Vector2[] ForceDirections = [Vector2.Right, Vector2.Down, Vector2.Left, Vector2.Up];

    [ExportGroup("Square Properties")]
    [Export] public float BaseSize = 50;
    [Export] public float SizeMultiplier = 100;
    [Export] public float MinSize = 30;
    [Export] public float MaxSize = 150;
    [Export] public float BaseForce = 15;
    [Export] public float ForceMultiplier = 130;
    [Export] public float TorqueMultiplier = 85;
    [Export] public float MinimumBassForForce = 0.05f;
    [Export] public float BassFrequencyMax = 250;
    [Export] public float MidFrequencyMax = 2000;
    [Export] public float SizeReactivity = 3;

    [ExportGroup("Impulses")]
    [Export] public float KickImpulse = 3200;
    [Export] public float KickTorqueImpulse = 1000;
    [Export] public float BassImpulse = 4000;
    [Export(PropertyHint.Range, "0.01,5")] public double BassImpulseIntervalSeconds = 1.0 / 6;
    [Export(PropertyHint.Range, "0.1,10")] public double KickIntervalSeconds = 2;
    [Export] public float MinimumSpeed = 100;

    [ExportGroup("References")]
    [Export] private Godot.Collections.Array<VisualizerSquare> _squares = [];
    [Export] private CollisionShape2D _topWall = null!;
    [Export] private CollisionShape2D _bottomWall = null!;
    [Export] private CollisionShape2D _leftWall = null!;
    [Export] private CollisionShape2D _rightWall = null!;

    private float _currentSize;
    private double _sinceKick;
    private double _sinceBassImpulse;
    private int _directionIndex;

    /// <inheritdoc />
    public override void Initialize(Vector2 viewportSize, AudioEffectSpectrumAnalyzerInstance spectrum)
    {
        base.Initialize(viewportSize, spectrum);
        _currentSize = BaseSize;
        _sinceKick = _sinceBassImpulse = 0;
        _directionIndex = 0;
        SetWall(_topWall, new Rect2(-BoundaryThickness, -BoundaryThickness, viewportSize.X + BoundaryThickness * 2, BoundaryThickness));
        SetWall(_bottomWall, new Rect2(-BoundaryThickness, viewportSize.Y, viewportSize.X + BoundaryThickness * 2, BoundaryThickness));
        SetWall(_leftWall, new Rect2(-BoundaryThickness, -BoundaryThickness, BoundaryThickness, viewportSize.Y + BoundaryThickness * 2));
        SetWall(_rightWall, new Rect2(viewportSize.X, -BoundaryThickness, BoundaryThickness, viewportSize.Y + BoundaryThickness * 2));
        for (int i = 0; i < _squares.Count; i++)
        {
            VisualizerSquare square = _squares[i];
            square.SetAppearance(_currentSize, LineColor);
            square.Position = new Vector2(viewportSize.X * (i + 1) / (_squares.Count + 1), viewportSize.Y / 2);
            square.LinearVelocity = Vector2.Zero;
            square.AngularVelocity = 0;
            KickStart(square);
        }
    }

    /// <inheritdoc />
    public override void PhysicsUpdate(double delta)
    {
        UpdateAudioReactivity(delta, SpectrumSampleCount, MagnitudeResponse);
        FinalColor = Color.FromHsv(ColorHue, 1, 1);
        float targetSize = Mathf.Clamp(BaseSize + SizeMultiplier * SmoothedMagnitude * SizeReactivity, MinSize, MaxSize);
        _currentSize = Mathf.Lerp(_currentSize, targetSize, 1 - Mathf.Exp(-SizeResponsePerSecond * (float)delta));

        _sinceBassImpulse += delta;
        _sinceKick += delta;
        bool applyBassImpulse = _sinceBassImpulse >= BassImpulseIntervalSeconds;
        bool checkSpeed = _sinceKick >= KickIntervalSeconds;
        if (applyBassImpulse)
            _sinceBassImpulse %= BassImpulseIntervalSeconds;
        if (checkSpeed)
            _sinceKick %= KickIntervalSeconds;

        float bass = GetFrequencyRangeMagnitude(MinimumFrequency, BassFrequencyMax);
        float mid = GetFrequencyRangeMagnitude(BassFrequencyMax, MidFrequencyMax);
        float high = GetFrequencyRangeMagnitude(MidFrequencyMax, MaximumFrequency);
        foreach (VisualizerSquare square in _squares)
        {
            square.SetAppearance(_currentSize, FinalColor);
            Vector2 direction = ForceDirections[_directionIndex].Rotated((float)GD.RandRange(-DirectionJitter, DirectionJitter));
            square.ApplyCentralForce(direction * (BaseForce + ForceMultiplier * (bass + mid * 0.5f)));
            square.ApplyTorque((mid - high) * TorqueMultiplier);
            if (applyBassImpulse && bass > MinimumBassForForce)
                square.ApplyCentralImpulse(direction * BassImpulse * bass);

            if (!new Rect2(-Vector2.One * ResetMargin, ViewportSize + Vector2.One * ResetMargin * 2).HasPoint(square.Position))
            {
                square.Position = ViewportSize / 2;
                square.LinearVelocity = Vector2.Zero;
                square.AngularVelocity = 0;
                KickStart(square);
            }
            else if (checkSpeed && square.LinearVelocity.Length() < MinimumSpeed)
                KickStart(square);
        }
        _directionIndex = (_directionIndex + 1) % ForceDirections.Length;
    }

    /// <summary>Fits an authored collision wall to the current viewport bounds.</summary>
    /// <param name="wall">Collision shape owned by a static boundary body.</param>
    /// <param name="bounds">Wall rectangle in visualization coordinates.</param>
    private static void SetWall(CollisionShape2D wall, Rect2 bounds)
    {
        wall.Position = bounds.GetCenter();
        ((RectangleShape2D)wall.Shape).Size = bounds.Size;
    }

    /// <summary>Gives an idle square a one-time linear and angular impulse.</summary>
    /// <param name="square">Body to start moving.</param>
    private void KickStart(VisualizerSquare square)
    {
        Vector2 direction = Vector2.Right.Rotated((float)GD.RandRange(0, Mathf.Tau));
        square.ApplyCentralImpulse(direction * KickImpulse);
        square.ApplyTorqueImpulse((float)GD.RandRange(-KickTorqueImpulse, KickTorqueImpulse));
    }
}
