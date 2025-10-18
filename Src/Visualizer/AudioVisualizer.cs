using System;
using System.Collections.Generic;
using Godot;

namespace GodAmp.Visualizer
{
    public partial class AudioVisualizer : Control
    {
        [Export] public Godot.Collections.Array<PackedScene> StrategyTypes;
        
        private SubViewportContainer _containerA;
        private SubViewportContainer _containerB;
        private SubViewport _viewportA;
        private SubViewport _viewportB;
        private ColorRect _rectA;
        private ColorRect _rectB;
        private VisualizerStrategy _strategyA;
        private VisualizerStrategy _strategyB;
        private Node2D _strategyContainerA;
        private Node2D _strategyContainerB;
        private Timer _timer;
        
        private ViewportTexture _previousFrameTexture;
        private ImageTexture _feedbackTexture;
        private Image _feedbackImage;
        private bool _isUsingA = true;
        private int _currStrategy = 0;
        
        private ShaderMaterial _shaderMaterialA;
        private ShaderMaterial _shaderMaterialB;

        // Properties for active viewport components
        private SubViewportContainer ActiveContainer => _isUsingA ? _containerA : _containerB;
        private SubViewportContainer InactiveContainer => _isUsingA ? _containerB : _containerA;
        private ShaderMaterial ActiveShader => _isUsingA ? _shaderMaterialA : _shaderMaterialB;
        private SubViewport ActiveViewport => _isUsingA ? _viewportA : _viewportB;
        private SubViewport InactiveViewport => _isUsingA ? _viewportB : _viewportA;
        private VisualizerStrategy ActiveStrategy => _isUsingA ? _strategyA : _strategyB;
        
        public override void _Ready()
        {
            _timer = GetNode<Timer>("Timer");
            InitializeViewports();
            InitializeStrategy(_viewportA.Size, _currStrategy);
            InitializeFeedbackTexture();
        }
        
        public override void _Process(double delta)
        {
            UpdateStrategy(delta);
            UpdateViewports();
            UpdateShaders();
            SwapViewports();
        }

        public void InitializeStrategy(Vector2 viewportSize, int currStrategy)
        {
            _strategyContainerA = GetNode<Node2D>("%StrategyContainerA");
            _strategyContainerB = GetNode<Node2D>("%StrategyContainerB");
            _strategyA?.QueueFree();
            _strategyB?.QueueFree();
            _strategyA = StrategyTypes[currStrategy].Instantiate<VisualizerStrategy>();
            _strategyB = StrategyTypes[currStrategy].Instantiate<VisualizerStrategy>();
            _strategyContainerA.AddChild(_strategyA);
            _strategyContainerB.AddChild(_strategyB);
            RefreshStrategy(viewportSize);
        }

        public void NextStrategy()
        {
            _currStrategy++;
            if (_currStrategy >= StrategyTypes.Count)
                _currStrategy = 0;
            InitializeStrategy(_viewportA.Size, _currStrategy);
            _timer.Start();
        }

        public void RefreshStrategy(Vector2 viewportSize)
        {
            _strategyA?.Initialize(viewportSize);
            _strategyB?.Initialize(viewportSize);
        }
        
        public void UpdateStrategy(double delta)
        {
            _strategyA.Update(delta);
            _strategyB.Update(delta);
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
            
            _shaderMaterialA.SetShaderParameter("previous_frame", _feedbackTexture);
            _shaderMaterialB.SetShaderParameter("previous_frame", _feedbackTexture);
        }
        
        private void UpdateViewports()
        {
            _viewportA.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            _viewportB.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }

        private void UpdateShaders()
        {
            Color dynamicColor = Color.FromHsv(ActiveStrategy.ColorHue, 0.8f, 1.0f);
            ActiveStrategy.FinalColor = dynamicColor;
            ActiveStrategy.FinalColor = dynamicColor;

            foreach (var material in new[] { _shaderMaterialA, _shaderMaterialB })
            {
                // Base parameters
                material.SetShaderParameter("glow_intensity", ActiveStrategy.GlowIntensity);
                material.SetShaderParameter("decay", ActiveStrategy.DecayRate * ActiveStrategy.FeedbackStrength);
                material.SetShaderParameter("color_decay", ActiveStrategy.ColorDecay);
                material.SetShaderParameter("trail_intensity", ActiveStrategy.TrailIntensity);
                material.SetShaderParameter("time_offset", ActiveStrategy.TimeOffset);
                material.SetShaderParameter("previous_frame", _feedbackTexture);
                
                // Audio-reactive parameters
                float audioModulatedDistortion = ActiveStrategy.Distortion + (ActiveStrategy.SmoothedMagnitude * 0.3f);
                float audioModulatedRotation = ActiveStrategy.RotationSpeed * (1.0f + ActiveStrategy.SmoothedMagnitude);
                
                material.SetShaderParameter("tunnel_depth", ActiveStrategy.TunnelDepth * (1.0f + ActiveStrategy.SmoothedDepth));
                material.SetShaderParameter("distortion", audioModulatedDistortion);
                material.SetShaderParameter("rotation_speed", audioModulatedRotation);
                material.SetShaderParameter("rotation_direction", ActiveStrategy.SmoothedDirection);
            }
        }
        private void SwapViewports()
        {
            // Capture the previous frame before swapping
            if (InactiveViewport.GetTexture() is ViewportTexture texture)
            {
                // Wait until viewport is done rendering
                RenderingServer.ForceSync();
                
                var viewportImage = texture.GetImage();
                if (viewportImage.GetFormat() != _feedbackImage.GetFormat())
                {
                    viewportImage.Convert(_feedbackImage.GetFormat());
                }
                
                _feedbackImage.CopyFrom(viewportImage);
                _feedbackTexture.Update(_feedbackImage);
            }
            
            // Swap z-indices
            ActiveContainer.ZIndex = 0;
            InactiveContainer.ZIndex = 1;
            
            _isUsingA = !_isUsingA;
        }
        
        private float GetViewportWidth() => _viewportA?.Size.X ?? 0;
        private float GetViewportHeight() => _viewportA?.Size.Y ?? 0;

        public void OnResized()
        {
            if (_viewportA == null || _viewportB == null)
                return;
            
            _viewportA.Size = new Vector2I((int)Size.X, (int)Size.Y);
            _viewportB.Size = new Vector2I((int)Size.X, (int)Size.Y);
            
            InitializeFeedbackTexture();
            RefreshStrategy(_viewportA.Size);
        }
    }
}