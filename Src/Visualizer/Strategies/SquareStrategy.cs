using Godot;

namespace GodAmp.Visualizer.Strategies;

public partial class SquareStrategy : VisualizerStrategy
{
    [ExportGroup("Square Properties")]
    [Export] public float BaseSize = 50.0f;
    [Export] public float SizeMultiplier = 100.0f;
    [Export] public float MinSize = 30.0f;
    [Export] public float MaxSize = 150.0f;
    [Export] public float BaseForce = 1000.0f;
    [Export] public float ForceMultiplier = 5000.0f;
    [Export] public float TorqueMultiplier = 2000.0f;
    [Export] public float MinimumBassForForce = 0.05f;
    [Export] public float BassFrequencyMax = 250.0f;
    [Export] public float MidFrequencyMax = 2000.0f;
    [Export] public float SizeReactivity = 3.0f;
    
    private RigidBody2D _body;
    private ColorRect _square;
    private float _currentSize;
    private float _timeAccumulator = 0.0f;
    private Vector2[] _forceDirections = new Vector2[4];
    private PhysicsMaterial _bouncyMaterial;
    
    public override void _Ready()
    {
        _body = GetNode<RigidBody2D>("RigidBody2D");
        _square = _body.GetNode<ColorRect>("Square");
        
        _forceDirections[0] = Vector2.Right;
        _forceDirections[1] = Vector2.Down;
        _forceDirections[2] = Vector2.Left;
        _forceDirections[3] = Vector2.Up;
    }
    
    public override void Initialize(Vector2 viewportSize)
    {
        base.Initialize(viewportSize);
        InitializeSquare();
        InitializePhysicsBoundaries(viewportSize);
        ResetPosition();
    }
    
    public override void Update(double delta)
    {
        FrameCount++;
        if (FrameCount % UpdateEveryNFrames != 0)
            return;
            
        _timeAccumulator += (float)delta;
        UpdateAudioReactivity(delta);
        UpdateSquare();
        ApplyAudioForces(delta);
        
        if (_timeAccumulator > 2.0f && _body.LinearVelocity.Length() < 100.0f)
        {
            KickStart();
            _timeAccumulator = 0.0f;
        }
    }
    
    private void InitializeSquare()
    {
        _currentSize = BaseSize;
        UpdateSquareSize();
        _square.Color = LineColor;
    }
    
    private void InitializePhysicsBoundaries(Vector2 viewportSize)
    {
        var walls = GetNode<Node2D>("Walls");
        
        // Clear any existing walls
        foreach (var child in walls.GetChildren())
        {
            child.QueueFree();
        }
        
        string[] wallNames = ["Top", "Bottom", "Left", "Right"];
        Vector2[] wallPositions =
        [
            new(viewportSize.X/2, -25),
            new(viewportSize.X/2, viewportSize.Y + 25),
            new(-25, viewportSize.Y/2),
            new(viewportSize.X + 25, viewportSize.Y/2)
        ];
        Vector2[] wallSizes =
        [
            new(viewportSize.X + 100, 50),
            new(viewportSize.X + 100, 50),
            new(50, viewportSize.Y + 100),
            new(50, viewportSize.Y + 100)
        ];
        
        for (int i = 0; i < 4; i++)
        {
            var wall = new StaticBody2D { Name = wallNames[i] };
            var collision = new CollisionShape2D();
            var shape = new RectangleShape2D();
            
            wall.AddChild(collision);
            collision.Shape = shape;
            walls.AddChild(wall);
            
            shape.Size = wallSizes[i];
            wall.Position = wallPositions[i];
        }
    }
    
    private void ResetPosition()
    {
        _body.Position = new Vector2(ViewportSize.X / 2, ViewportSize.Y / 2);
        _body.LinearVelocity = Vector2.Zero;
        _body.AngularVelocity = 0;
        
        KickStart();
    }
    
    private void KickStart()
    {
        var impulseDir = Vector2.Right.Rotated((float)GD.RandRange(0, Mathf.Pi * 2));
        _body.ApplyCentralImpulse(impulseDir * BaseForce * 4.0f);
        _body.ApplyTorqueImpulse((float)GD.RandRange(-1000, 1000));
    }
    
    private void UpdateSquare()
    {
        // Amplify the audio effect on size with SizeReactivity
        float targetSize = Mathf.Clamp(
            BaseSize + (SizeMultiplier * SmoothedMagnitude * SizeReactivity),
            MinSize,
            MaxSize
        );
        
        // More responsive size change
        _currentSize = Mathf.Lerp(_currentSize, targetSize, 0.3f);
        UpdateSquareSize();
        
        _square.Color = FinalColor;
        
        if (_body.Position.X < -100 || _body.Position.X > ViewportSize.X + 100 ||
            _body.Position.Y < -100 || _body.Position.Y > ViewportSize.Y + 100)
        {
            ResetPosition();
        }
    }
    
    private void UpdateSquareSize()
    {
        _square.Size = new Vector2(_currentSize, _currentSize);
        _square.Position = new Vector2(-_currentSize/2, -_currentSize/2);
        
        var collisionShape = _body.GetNode<CollisionShape2D>("CollisionShape2D");
        var shape = (RectangleShape2D)collisionShape.Shape;
        shape.Size = new Vector2(_currentSize, _currentSize);
    }
    
    private void ApplyAudioForces(double delta)
    {
        var bassFreq = GetFrequencyRangeMagnitude(20, BassFrequencyMax);
        var midFreq = GetFrequencyRangeMagnitude(BassFrequencyMax, MidFrequencyMax);
        var highFreq = GetFrequencyRangeMagnitude(MidFrequencyMax, 20000);
        
        // Always apply some force based on any audio, not just bass
        float forceMagnitude = BaseForce + (ForceMultiplier * (bassFreq + midFreq * 0.5f));
        var forceDir = _forceDirections[FrameCount % 4].Rotated((float)GD.RandRange(-0.5, 0.5));
        _body.ApplyCentralForce(forceDir * forceMagnitude * (float)delta);
        
        // Apply stronger impulse on bass hits
        if (bassFreq > MinimumBassForForce && FrameCount % 10 == 0)
        {
            _body.ApplyCentralImpulse(forceDir * BaseForce * bassFreq * 5.0f);
        }
        
        var torque = (midFreq - highFreq) * TorqueMultiplier;
        _body.ApplyTorque(torque * (float)delta);
        
        if (_body.LinearDamp > 0.05f)
            _body.LinearDamp = 0.05f + midFreq * 0.1f;
        
        if (_body.AngularDamp > 0.05f)
            _body.AngularDamp = 0.05f + highFreq * 0.1f;
    }
    
    private static float GetFrequencyForSampleIndex(int index, int sampleCount)
    {
        return Mathf.Exp(Mathf.Lerp(Mathf.Log(20f), Mathf.Log(22050f), (float)index / sampleCount));
    }
    
    private float GetFrequencyRangeMagnitude(float minHz, float maxHz)
    {
        var magnitude = Spectrum.GetMagnitudeForFrequencyRange(minHz, maxHz);
        return (magnitude.X + magnitude.Y) * 0.5f;
    }
    
    private void UpdateAudioReactivity(double delta)
    {
        const int sampleCount = 64;
        float sum = 0f;
        
        for (int i = 0; i < sampleCount; i++)
        {
            float hzMin = GetFrequencyForSampleIndex(i, sampleCount);
            float hzMax = GetFrequencyForSampleIndex(i + 1, sampleCount);
            sum += GetFrequencyRangeMagnitude(hzMin, hzMax);
        }
        
        float currentMagnitude = sum / sampleCount;
        // Use higher smoothing factor for more responsive size changes
        SmoothedMagnitude = Mathf.Lerp(SmoothedMagnitude, currentMagnitude, SmoothingFactor * 2.0f);
        
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
        
        ColorHue = (ColorHue + ColorChangeSpeed * (float)delta) % 1.0f;
        FinalColor = Color.FromHsv(ColorHue, 1.0f, 1.0f, 1.0f);
    }
} 