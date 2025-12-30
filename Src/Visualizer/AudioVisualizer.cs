using System.Collections.Generic;
using System.Linq;
using GodAmp.Data;
using Godot;

namespace GodAmp.Visualizer
{
    public partial class AudioVisualizer : Control
    {
        [Export(PropertyHint.Dir)] private string _strategyTypeDirectory = null!;

        public Dictionary<StringName, VisualizerStrategyType> StrategyTypeMap = new();

        private SubViewportContainer? _containerA;
        private SubViewportContainer? _containerB;
        private SubViewport? _viewportA;
        private SubViewport? _viewportB;
        private ColorRect? _rectA;
        private ColorRect? _rectB;
        private VisualizerStrategy? _strategyA;
        private VisualizerStrategy? _strategyB;
        private Node2D? _strategyContainerA;
        private Node2D? _strategyContainerB;

        private ImageTexture? _feedbackTexture;
        private Image? _feedbackImage;
        private bool _isUsingA = true;

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
            LoadStrategiesFromDisk();
            InitializeViewports();

            if (StrategyTypeMap.Count > 0 && _viewportA is { } vp)
                InitializeStrategy(vp.Size, StrategyTypeMap.First().Key);

            InitializeFeedbackTexture();
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
            _strategyContainerA ??= GetNode<Node2D>("%StrategyContainerA");
            _strategyContainerB ??= GetNode<Node2D>("%StrategyContainerB");
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
            _strategyA?.Initialize(viewportSize);
            _strategyB?.Initialize(viewportSize);
        }

        private void UpdateStrategy(double delta)
        {
            _strategyA?.Update(delta);
            _strategyB?.Update(delta);
        }

        private void InitializeViewports()
        {
            _containerA = GetNode<SubViewportContainer>("ContainerA");
            _containerB = GetNode<SubViewportContainer>("ContainerB");
            _viewportA = _containerA.GetNode<SubViewport>("SubViewport");
            _viewportB = _containerB.GetNode<SubViewport>("SubViewport");
            _rectA = _viewportA.GetNode<ColorRect>("ColorRect");
            _rectB = _viewportB.GetNode<ColorRect>("ColorRect");

            foreach (var viewport in new[] { _viewportA, _viewportB })
            {
                viewport.RenderTargetClearMode = SubViewport.ClearMode.Never;
                viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                viewport.GetNode<ColorRect>("Background").Color = Colors.Black;
            }

            _shaderMaterialA = (ShaderMaterial)_rectA.Material;
            _shaderMaterialB = (ShaderMaterial)_rectB.Material;

            _containerA.Visible = true;
            _containerB.Visible = true;
        }

        private void InitializeFeedbackTexture()
        {
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

                var viewportImage = texture.GetImage();
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
            if (_viewportA is not { } vpA || _viewportB is not { } vpB)
                return;

            var newSize = new Vector2I((int)Size.X, (int)Size.Y);
            vpA.Size = newSize;
            vpB.Size = newSize;

            InitializeFeedbackTexture();
            RefreshStrategy(newSize);
        }

        private void LoadStrategiesFromDisk()
        {
            StrategyTypeMap = DirAccess.GetFilesAt(_strategyTypeDirectory)
                .Where(f => f.EndsWith(".tres") || f.EndsWith(".res"))
                .Select(f => GD.Load<VisualizerStrategyType>(_strategyTypeDirectory.PathJoin(f)))
                .Where(r => r != null)
                .ToDictionary(s => s.Id);
        }
    }
}