using System.Collections.Generic;
using System;
using System.Linq;
using GodAmp.Data;
using GodAmp.Audio.Processing;
using Godot;

namespace GodAmp.Visualizer
{
    public partial class AudioVisualizer : Control
    {
        [Export(PropertyHint.Dir)] private string _strategyTypeDirectory = null!;
        [Export] private AudioController _audio = null!;

        public Dictionary<StringName, VisualizerStrategyType> StrategyTypeMap = [];

        [Export] private SubViewportContainer _containerA = null!;
        [Export] private SubViewportContainer _containerB = null!;
        [Export] private SubViewport _viewportA = null!;
        [Export] private SubViewport _viewportB = null!;
        [Export] private ColorRect _rectA = null!;
        [Export] private ColorRect _rectB = null!;
        private VisualizerStrategy? _strategyA;
        private VisualizerStrategy? _strategyB;
        [Export] private Node2D _strategyContainerA = null!;
        [Export] private Node2D _strategyContainerB = null!;

        private ImageTexture? _feedbackTexture;
        private Image? _feedbackImage;
        private bool _isUsingA = true;
        private bool _initialized;

        private ShaderMaterial? _shaderMaterialA;
        private ShaderMaterial? _shaderMaterialB;

        private SubViewportContainer? ActiveContainer => _isUsingA ? _containerA : _containerB;
        private SubViewportContainer? InactiveContainer => _isUsingA ? _containerB : _containerA;
        private SubViewport? ActiveViewport => _isUsingA ? _viewportA : _viewportB;
        private SubViewport? InactiveViewport => _isUsingA ? _viewportB : _viewportA;
        private VisualizerStrategy? ActiveStrategy => _isUsingA ? _strategyA : _strategyB;

        private float GetViewportWidth() => _viewportA?.Size.X ?? 0;
        private float GetViewportHeight() => _viewportA?.Size.Y ?? 0;

        public override void _Ready()
        {
            LoadStrategies();
            _shaderMaterialA = (ShaderMaterial)_rectA.Material;
            _shaderMaterialB = (ShaderMaterial)_rectB.Material;
            _initialized = true;
            OnResized();

            if (StrategyTypeMap.Count > 0 && _viewportA is { } vp)
                InitializeStrategy(vp.Size, StrategyTypeMap.First().Key);

        }

        public override void _Process(double delta)
        {
            if (_viewportA == null || _viewportB == null)
                return;

            UpdateStrategy(delta);
            UpdateViewports();
            UpdateShaders();
            SwapViewports();
        }

        private void InitializeStrategy(Vector2 viewportSize, StringName strategyId)
        {
            _strategyA?.QueueFree();
            _strategyB?.QueueFree();
            _strategyA = StrategyTypeMap[strategyId].Scene.Instantiate<VisualizerStrategy>();
            _strategyB = StrategyTypeMap[strategyId].Scene.Instantiate<VisualizerStrategy>();
            _strategyContainerA?.AddChild(_strategyA);
            _strategyContainerB?.AddChild(_strategyB);
            RefreshStrategy(viewportSize);
        }

        public void SwitchStrategy(StringName strategyId)
        {
            if (_viewportA is { } vp)
                InitializeStrategy(vp.Size, strategyId);
        }

        private void RefreshStrategy(Vector2 viewportSize)
        {
            _strategyA?.Initialize(viewportSize, _audio.Spectrum);
            _strategyB?.Initialize(viewportSize, _audio.Spectrum);
        }

        private void UpdateStrategy(double delta)
        {
            _strategyA?.Update(delta);
            _strategyB?.Update(delta);
        }

        private void InitializeFeedbackTexture()
        {
            _feedbackTexture?.Dispose();
            _feedbackImage?.Dispose();
            _feedbackImage = Image.CreateEmpty((int)GetViewportWidth(), (int)GetViewportHeight(), false, Image.Format.Rgba8);
            _feedbackTexture = ImageTexture.CreateFromImage(_feedbackImage);

            _shaderMaterialA?.SetShaderParameter("previous_frame", _feedbackTexture);
            _shaderMaterialB?.SetShaderParameter("previous_frame", _feedbackTexture);
        }

        private void UpdateViewports()
        {
            if (_viewportA is { } vpA)
                vpA.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            if (_viewportB is { } vpB)
                vpB.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }

        private void UpdateShaders()
        {
            if (ActiveStrategy is not { } strategy || _feedbackTexture == null)
                return;

            Color dynamicColor = Color.FromHsv(strategy.ColorHue, 0.8f, 1.0f);
            strategy.FinalColor = dynamicColor;

            foreach (var material in new[] { _shaderMaterialA, _shaderMaterialB })
            {
                if (material == null) continue;

                material.SetShaderParameter("glow_intensity", strategy.GlowIntensity);
                material.SetShaderParameter("decay", strategy.DecayRate * strategy.FeedbackStrength);
                material.SetShaderParameter("color_decay", strategy.ColorDecay);
                material.SetShaderParameter("trail_intensity", strategy.TrailIntensity);
                material.SetShaderParameter("time_offset", strategy.TimeOffset);
                material.SetShaderParameter("previous_frame", _feedbackTexture);

                float audioModulatedDistortion = strategy.Distortion + (strategy.SmoothedMagnitude * 0.3f);
                float audioModulatedRotation = strategy.RotationSpeed * (1.0f + strategy.SmoothedMagnitude);

                material.SetShaderParameter("tunnel_depth", strategy.TunnelDepth * (1.0f + strategy.SmoothedDepth));
                material.SetShaderParameter("distortion", audioModulatedDistortion);
                material.SetShaderParameter("rotation_speed", audioModulatedRotation);
                material.SetShaderParameter("rotation_direction", strategy.SmoothedDirection);
            }
        }
        private void SwapViewports()
        {
            if (InactiveViewport?.GetTexture() is { } texture && _feedbackImage is { } feedbackImg && _feedbackTexture is { } feedbackTex)
            {
                RenderingServer.ForceSync();

                using var viewportImage = texture.GetImage();
                if (viewportImage.GetFormat() != feedbackImg.GetFormat())
                    viewportImage.Convert(feedbackImg.GetFormat());

                feedbackImg.CopyFrom(viewportImage);
                feedbackTex.Update(feedbackImg);
            }

            if (ActiveContainer is { } active)
                active.ZIndex = 0;
            if (InactiveContainer is { } inactive)
                inactive.ZIndex = 1;

            _isUsingA = !_isUsingA;
        }

        private void OnResized()
        {
            if (!_initialized || _viewportA is not { } vpA || _viewportB is not { } vpB)
                return;

            var newSize = new Vector2I(Math.Max(1, (int)Size.X), Math.Max(1, (int)Size.Y));
            vpA.Size = newSize;
            vpB.Size = newSize;

            InitializeFeedbackTexture();
            RefreshStrategy(newSize);
        }

        /// <inheritdoc />
        public override void _ExitTree()
        {
            _shaderMaterialA?.SetShaderParameter("previous_frame", default);
            _shaderMaterialB?.SetShaderParameter("previous_frame", default);
            _feedbackTexture?.Dispose();
            _feedbackImage?.Dispose();
        }

        /// <summary>Discovers strategy resources using paths that resolve in both the editor and exported packs.</summary>
        private void LoadStrategies()
        {
            StrategyTypeMap = ResourceLoader.ListDirectory(_strategyTypeDirectory)
                .Where(f => f.EndsWith(".tres", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".res", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(f => GD.Load<VisualizerStrategyType>(_strategyTypeDirectory.PathJoin(f)))
                .Where(r => r != null)
                .ToDictionary(s => s.Id);
        }
    }
}
