using System;
using System.Collections.Generic;
using System.Linq;
using GodAmp.Audio.Processing;
using Godot;

namespace GodAmp.Presentation.Visualizer;

/// <summary>Renders one visualization world into alternating GPU feedback buffers.</summary>
public partial class AudioVisualizer : Control
{
    [Export(PropertyHint.Dir)] private string _strategyTypeDirectory = null!;
    [Export] private AudioController _audio = null!;
    [Export] private SubViewport _viewportA = null!;
    [Export] private SubViewport _viewportB = null!;
    [Export] private ColorRect _passA = null!;
    [Export] private ColorRect _passB = null!;
    [Export] private TextureRect _display = null!;
    [Export] private Node2D _strategyContainer = null!;

    /// <summary>Gets discovered strategy resources in their menu order.</summary>
    public IReadOnlyDictionary<StringName, VisualizerStrategyType> StrategyTypeMap { get; private set; }
        = new Dictionary<StringName, VisualizerStrategyType>().AsReadOnly();

    private VisualizerStrategy? _strategy;
    private ShaderMaterial _materialA = null!;
    private ShaderMaterial _materialB = null!;
    private bool _writeA = true;
    private bool _hasHistory;
    private bool _framePending;
    private bool _initialized;

    /// <inheritdoc />
    public override void _Ready()
    {
        _viewportB.World2D = _viewportA.World2D;
        LoadStrategies();
        _materialA = (ShaderMaterial)_passA.Material;
        _materialB = (ShaderMaterial)_passB.Material;
        RenderingServer.FramePostDraw += OnFrameRendered;
        _initialized = true;
        OnResized();
        if (StrategyTypeMap.Count > 0)
            SwitchStrategy(StrategyTypeMap.First().Key);
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_strategy == null)
            return;
        _strategy.Update(delta);
        SubViewport destination = _writeA ? _viewportA : _viewportB;
        SubViewport source = _writeA ? _viewportB : _viewportA;
        ShaderMaterial material = _writeA ? _materialA : _materialB;
        ShaderMaterial inactiveMaterial = _writeA ? _materialB : _materialA;

        /* Only the destination renders. The other texture retains the completed previous frame. */
        source.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        inactiveMaterial.SetShaderParameter("previous_frame", default);
        material.SetShaderParameter("previous_frame", _hasHistory ? source.GetTexture() : default(Variant));
        UpdateShader(material, _strategy);
        destination.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        _display.Texture = destination.GetTexture();
        _framePending = true;
    }

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta) => _strategy?.PhysicsUpdate(delta);

    /// <summary>Starts or suspends simulation and all pending render passes.</summary>
    /// <param name="active">Whether visible, playing content should advance.</param>
    public void SetActive(bool active)
    {
        ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        if (!active)
        {
            _viewportA.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            _viewportB.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            _framePending = false;
            _display.Texture = _hasHistory ? (_writeA ? _viewportB : _viewportA).GetTexture() : null;
        }
    }

    /// <summary>Promotes a completed GPU pass to history before selecting the next destination.</summary>
    private void OnFrameRendered()
    {
        if (!_framePending)
            return;
        _framePending = false;
        _hasHistory = true;
        _writeA = !_writeA;
    }

    /// <summary>Replaces the active strategy and clears feedback from the preceding visualization.</summary>
    /// <param name="strategyId">Identifier of a discovered strategy resource.</param>
    public void SwitchStrategy(StringName strategyId)
    {
        if (_strategy != null)
        {
            _strategyContainer.RemoveChild(_strategy);
            _strategy.QueueFree();
        }
        _strategy = StrategyTypeMap[strategyId].Scene.Instantiate<VisualizerStrategy>();
        _strategyContainer.AddChild(_strategy);
        _strategy.Initialize(_viewportA.Size, _audio.Spectrum);
        ResetFeedback();
    }

    /// <summary>Applies the active strategy's warp and decay to the destination pass.</summary>
    /// <param name="material">Material belonging exclusively to the destination viewport.</param>
    /// <param name="strategy">Simulation supplying the current audio-reactive parameters.</param>
    private static void UpdateShader(ShaderMaterial material, VisualizerStrategy strategy)
    {
        material.SetShaderParameter("decay", strategy.DecayRate * strategy.FeedbackStrength);
        material.SetShaderParameter("time_offset", strategy.TimeOffset);
        material.SetShaderParameter("tunnel_depth", strategy.TunnelDepth * (1 + strategy.SmoothedDepth));
        material.SetShaderParameter("rotation_speed", strategy.RotationSpeed * (1 + strategy.SmoothedMagnitude));
        material.SetShaderParameter("rotation_direction", strategy.SmoothedDirection);
    }

    /// <summary>Resizes both buffers together and resets simulation geometry and feedback.</summary>
    private void OnResized()
    {
        if (!_initialized)
            return;
        var size = new Vector2I(Math.Max(1, (int)Size.X), Math.Max(1, (int)Size.Y));
        if (_viewportA.Size == size && _viewportB.Size == size)
            return;
        _viewportA.Size = _viewportB.Size = size;
        _strategy?.Initialize(size, _audio.Spectrum);
        ResetFeedback();
    }

    /// <summary>Invalidates history without reading or allocating CPU images.</summary>
    private void ResetFeedback()
    {
        _hasHistory = false;
        _framePending = false;
        _writeA = true;
        _display.Texture = null;
        _viewportA.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        _viewportB.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        _materialA.SetShaderParameter("previous_frame", default);
        _materialB.SetShaderParameter("previous_frame", default);
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        RenderingServer.FramePostDraw -= OnFrameRendered;
        if (_initialized)
            ResetFeedback();
    }

    /// <summary>Discovers strategy resources in a stable order in the editor and exported packs.</summary>
    private void LoadStrategies()
    {
        StrategyTypeMap = ResourceLoader.ListDirectory(_strategyTypeDirectory)
            .Where(file => file.EndsWith(".tres", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".res", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(file => GD.Load<VisualizerStrategyType>(_strategyTypeDirectory.PathJoin(file)))
            .ToDictionary(strategy => strategy.Id).AsReadOnly();
    }
}
