using System;
using GodAmp.Autoload;
using GodAmp.Core;
using GodAmp.Data;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.MasterPanel;

/// <summary>Renders the classic spectrum and PCM oscilloscope in a skin-colored pixel buffer.</summary>
public partial class ClassicVisualization : Control
{
    /// <summary>Display modes selected by clicking the main-panel visualization.</summary>
    public enum VisualizationMode { Spectrum, Oscilloscope, Off }

    private const int PixelWidth = 76;
    private const int PixelHeight = 16;
    private const int BarStride = 4;
    private const int BarCount = PixelWidth / BarStride;
    private const int WindowshadeWidth = 38;
    private const int WindowshadeHeight = 5;
    private const float MinimumFrequency = 20;
    private const float MaximumFrequency = 16000;
    private const float SpectrumRangeDb = 60;
    private const float BarFalloffPerSecond = 24;
    private const float PeakFalloffPerSecond = 8;
    private const float ScopeDurationSeconds = 0.012f;
    private readonly float[] _levels = new float[BarCount];
    private readonly float[] _peaks = new float[BarCount];
    private readonly float[] _scope = new float[PixelWidth - 1];
    private AudioEffectSpectrumAnalyzerInstance _analyzer = null!;
    private AudioEffectCapture _capture = null!;
    private Image _image = null!;
    private ImageTexture _texture = null!;
    private Image _windowshadeImage = null!;
    private ImageTexture _windowshadeTexture = null!;

    /// <summary>Compact scene display sharing this analyzer and capture buffer.</summary>
    [Export] public TextureRect WindowshadeDisplay { get; set; } = null!;

    /// <summary>Visualization displayed in the main panel.</summary>
    [Export] public VisualizationMode Mode { get; set; }
    /// <summary>Runtime player reference used to clear stale samples on pause and stop.</summary>
    public TrackPlayer? Player { get; set; }

    /// <inheritdoc />
    public override void _Ready()
    {
        int bus = AudioServer.GetBusIndex("Master");
        _analyzer = (AudioEffectSpectrumAnalyzerInstance)AudioServer.GetBusEffectInstance(bus, AudioUtils.SpectrumAnalyzerAudioEffectIndex);
        _capture = (AudioEffectCapture)AudioServer.GetBusEffect(bus, AudioUtils.OscilloscopeAudioEffectIndex);
        _image = Image.CreateEmpty(PixelWidth, PixelHeight, false, Image.Format.Rgba8);
        _texture = ImageTexture.CreateFromImage(_image);
        _windowshadeImage = Image.CreateEmpty(WindowshadeWidth, WindowshadeHeight, false, Image.Format.Rgba8);
        _windowshadeTexture = ImageTexture.CreateFromImage(_windowshadeImage);
        WindowshadeDisplay.Texture = _windowshadeTexture;
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _texture.Dispose();
        _image.Dispose();
        _windowshadeTexture.Dispose();
        _windowshadeImage.Dispose();
    }

    /// <summary>Cycles spectrum, oscilloscope, and off when the display is clicked.</summary>
    /// <param name="input">GUI event routed by the scene connection.</param>
    private void OnGuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            return;
        Mode = (VisualizationMode)(((int)Mode + 1) % Enum.GetValues<VisualizationMode>().Length);
        Array.Clear(_levels);
        Array.Clear(_peaks);
        AcceptEvent();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        bool playing = Player is { Playing: true, StreamPaused: false };
        ReadWaveform(playing && Mode == VisualizationMode.Oscilloscope);
        VisualizationPalette palette = SkinLoader.Instance.Palette;
        _image.Fill(palette[0]);
        if (Mode != VisualizationMode.Off)
        {
            for (int y = 0; y < PixelHeight; y += 2)
                for (int x = 0; x < PixelWidth; x += 2)
                    _image.SetPixel(x, y, palette[1]);
            if (Mode == VisualizationMode.Spectrum)
                DrawSpectrum(palette, playing, (float)delta);
            else
                DrawWaveform(palette);
        }
        _texture.Update(_image);
        DrawWindowshade(palette);
        QueueRedraw();
    }

    /// <summary>Renders the compact spectrum or waveform from the same audio samples and decay state.</summary>
    /// <param name="palette">The active skin's classic visualization colors.</param>
    private void DrawWindowshade(VisualizationPalette palette)
    {
        _windowshadeImage.Fill(palette[0]);
        int previous = -1;
        for (int x = 0; x < WindowshadeWidth; x++)
        {
            if (Mode == VisualizationMode.Spectrum && x % BarStride != BarStride - 1)
            {
                int band = Math.Min(BarCount - 1, x / BarStride * 2);
                int height = (int)(_levels[band] * WindowshadeHeight / PixelHeight);
                for (int y = WindowshadeHeight - height; y < WindowshadeHeight; y++)
                    _windowshadeImage.SetPixel(x, y, palette[17 - (WindowshadeHeight - 1 - y) * 3]);
                int peak = (int)(_peaks[band] * WindowshadeHeight / PixelHeight);
                if (peak > 0)
                    _windowshadeImage.SetPixel(x, Math.Max(0, WindowshadeHeight - peak), palette[23]);
            }
            else if (Mode == VisualizationMode.Oscilloscope)
            {
                int sample = x * (_scope.Length - 1) / (WindowshadeWidth - 1);
                int y = Mathf.Clamp((int)((1 - _scope[sample]) * WindowshadeHeight / 2), 0, WindowshadeHeight - 1);
                if (previous < 0)
                    previous = y;
                for (int row = Math.Min(y, previous); row <= Math.Max(y, previous); row++)
                    _windowshadeImage.SetPixel(x, row, palette[18]);
                previous = y;
            }
        }
        _windowshadeTexture.Update(_windowshadeImage);
    }

    /// <summary>Consumes buffered stereo samples and resamples a rising-edge-triggered mono waveform.</summary>
    /// <param name="enabled">Whether live waveform samples should be displayed.</param>
    private void ReadWaveform(bool enabled)
    {
        if (!enabled)
        {
            _capture.ClearBuffer();
            Array.Clear(_scope);
            return;
        }
        int available = _capture.GetFramesAvailable();
        if (available < 2)
            return;
        Vector2[] samples = _capture.GetBuffer(available);
        int length = Math.Min(samples.Length, (int)(AudioServer.GetMixRate() * ScopeDurationSeconds));
        int start = samples.Length - length;
        for (int i = start; i > 0 && i > start - length; i--)
        {
            if (samples[i - 1].X + samples[i - 1].Y <= 0 && samples[i].X + samples[i].Y > 0)
            {
                start = i;
                break;
            }
        }
        for (int x = 0; x < _scope.Length; x++)
        {
            Vector2 sample = samples[start + x * (length - 1) / (_scope.Length - 1)];
            _scope[x] = (sample.X + sample.Y) / 2;
        }
    }

    /// <summary>Maps logarithmic frequency bands to decibel heights with time-based decay and peak markers.</summary>
    /// <param name="palette">Active skin colors.</param>
    /// <param name="playing">Whether the source is playing rather than paused or stopped.</param>
    /// <param name="delta">Elapsed seconds, used for frame-rate-independent decay.</param>
    private void DrawSpectrum(VisualizationPalette palette, bool playing, float delta)
    {
        float upperFrequency = Mathf.Min(MaximumFrequency, AudioServer.GetMixRate() / 2);
        for (int band = 0; band < BarCount; band++)
        {
            float low = MinimumFrequency * Mathf.Pow(upperFrequency / MinimumFrequency, (float)band / BarCount);
            float high = MinimumFrequency * Mathf.Pow(upperFrequency / MinimumFrequency, (float)(band + 1) / BarCount);
            Vector2 magnitude = playing ? _analyzer.GetMagnitudeForFrequencyRange(low, high) : Vector2.Zero;
            float amplitude = Mathf.Max(magnitude.X, magnitude.Y);
            float target = amplitude > 0 ? Mathf.Clamp((Mathf.LinearToDb(amplitude) + SpectrumRangeDb) / SpectrumRangeDb, 0, 1) * PixelHeight : 0;
            _levels[band] = Mathf.Max(target, _levels[band] - delta * BarFalloffPerSecond);
            _peaks[band] = Mathf.Max(_levels[band], _peaks[band] - delta * PeakFalloffPerSecond);
            int height = (int)_levels[band];
            for (int column = 0; column < BarStride - 1; column++)
            {
                int x = band * BarStride + column;
                for (int y = PixelHeight - height; y < PixelHeight; y++)
                    _image.SetPixel(x, y, palette[2 + y]);
                if (_peaks[band] >= 1)
                    _image.SetPixel(x, Mathf.Clamp(PixelHeight - (int)_peaks[band], 0, PixelHeight - 1), palette[23]);
            }
        }
    }

    /// <summary>Draws connected waveform samples using the five classic scope colors.</summary>
    /// <param name="palette">Active skin colors.</param>
    private void DrawWaveform(VisualizationPalette palette)
    {
        int previous = -1;
        for (int x = 0; x < _scope.Length; x++)
        {
            int y = Mathf.Clamp((int)((1 - _scope[x]) * PixelHeight / 2), 0, PixelHeight - 1);
            int top = previous < 0 ? y : Math.Min(y, previous);
            int bottom = previous < 0 ? y : Math.Max(y, previous);
            Color color = palette[18 + Mathf.Abs(y / 2 - 4)];
            for (int row = top; row <= bottom; row++)
                _image.SetPixel(x, row, color);
            previous = y;
        }
    }

    /// <inheritdoc />
    public override void _Draw() => DrawTexture(_texture, Vector2.Zero);
}
