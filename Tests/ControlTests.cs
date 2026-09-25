using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GodAmp.Autoload;
using GodAmp.Components;
using GodAmp.Controls.MasterPanel;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Tests;

/// <summary>Exercises classic control geometry, mouse interactions, and activation through real scenes.</summary>
public partial class ControlTests : Node
{
    private const int SettleFrames = 3;
    private Node _app = null!;
    private SubViewport _inputViewport = null!;
    private Control _mainControls = null!;
    private Control _equalizerControls = null!;

    /// <inheritdoc />
    public override void _Ready() => Callable.From(Run).CallDeferred();

    /// <summary>Runs the default and supplied skins at both reference scales with isolated settings.</summary>
    private async void Run()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(OS.GetEnvironment("GODAMP_DATA_DIR")))
                throw new InvalidOperationException("Set GODAMP_DATA_DIR to an isolated test directory.");
            Require(DisplayServer.GetName() != "headless", "ControlTests requires a graphical desktop; omit --headless.");
            _app = GD.Load<PackedScene>("res://Src/Main.tscn").Instantiate();
            AddChild(_app);
            _app.GetNode<MasterPanel>("MasterPanel").ClockBlinkEverySeconds = float.MaxValue;
            _app.GetNode<Timer>("MasterPanel/TextDisplay/MasterLabel/Timer").Stop();
            _inputViewport = new SubViewport { Size = new Vector2I(550, 232), HandleInputLocally = true };
            AddChild(_inputViewport);
            _mainControls = CreateInputControls("res://Src/Controls/MasterPanel/MasterPanel.tscn");
            _equalizerControls = CreateInputControls("res://Src/Controls/Equalizer/Equalizer.tscn");
            _inputViewport.NotifyMouseEntered();
            foreach (string skin in SkinLoader.GetAvailableSkins().Prepend(""))
            {
                if (skin.Length == 0)
                    SkinLoader.RestoreOriginalSkin();
                else
                    Require(SkinLoader.Load(Path.Combine(SkinLoader.GetSkinsDirectory(), skin)), SkinLoader.Instance.LastError ?? "Skin load failed.");
                foreach (int scale in new[] { 1, 2 })
                {
                    SignalBus.Instance.EmitSignal(SignalBus.SignalName.ZoomModeRequested, scale);
                    _mainControls.Scale = _equalizerControls.Scale = Vector2.One * scale;
                    await Settle();
                    CheckGeometry();
                    CheckAudioConnections();
                    await CheckSliders();
                    await CheckToggles();
                    CheckTitlebars();
                    GD.Print($"PASS: control states {skin} at {scale}x");
                }
            }
            SkinLoader.RestoreOriginalSkin();
            CheckTitlebars();
            GD.Print("PASS: classic control regression tests");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PrintErr(error);
            GetTree().Quit(1);
        }
    }

    /// <summary>Instantiates scene controls in a viewport isolated from native OS mouse movement.</summary>
    /// <param name="scenePath">Panel scene supplying the controls and their original sprite resources.</param>
    /// <returns>The controls with playback callbacks removed along with the unused panel.</returns>
    private Control CreateInputControls(string scenePath)
    {
        var panel = GD.Load<PackedScene>(scenePath).Instantiate();
        var controls = panel.GetNode<Control>("Controls");
        panel.RemoveChild(controls);
        panel.Free();
        _inputViewport.AddChild(controls);
        return controls;
    }

    /// <summary>Asserts reference rectangles independently of Godot container sizing.</summary>
    private void CheckGeometry()
    {
        CheckRect("MasterPanel/Controls/WinampMenuButton", new Rect2(6, 3, 9, 9));
        CheckRect("MasterPanel/Controls/CloseButton", new Rect2(264, 3, 9, 9));
        CheckRect("EqualizerWindow/Equalizer/Controls/CloseButton", new Rect2(264, 3, 9, 9));
        CheckRect("MasterPanel/Controls/VolumeSlider", new Rect2(107, 57, 68, 13));
        CheckRect("MasterPanel/Controls/PannerAudioSlider", new Rect2(177, 57, 38, 13));
        CheckRect("MasterPanel/Controls/PositionSeeker", new Rect2(16, 72, 248, 10));
        CheckRect("MasterPanel/Controls/ToggleEqualizerButton", new Rect2(219, 58, 23, 12));
        CheckRect("MasterPanel/Controls/TogglePlaylistButton", new Rect2(242, 58, 23, 12));
        string[] transport = ["PreviousTrackButton", "PlayTrackButton", "PauseTrackButton", "StopTrackButton", "NextTrackButton"];
        for (int i = 0; i < transport.Length; i++)
            CheckRect("MasterPanel/Controls/" + transport[i], new Rect2(16 + i * 23, 88, i == 4 ? 22 : 23, 18));
        CheckRect("MasterPanel/Controls/LoadTracksButton", new Rect2(136, 89, 22, 16));
        CheckRect("MasterPanel/Controls/ShuffleModeButton", new Rect2(164, 89, 47, 15));
        CheckRect("MasterPanel/Controls/RepeatModeButton", new Rect2(210, 89, 28, 15));
        CheckRect("EqualizerWindow/Equalizer/Controls/EqualizerToggleButton", new Rect2(14, 18, 25, 12));
        CheckRect("EqualizerWindow/Equalizer/Controls/AutoEqualizeToggleButton", new Rect2(39, 18, 33, 12));
        CheckRect("EqualizerWindow/Equalizer/Controls/PresetsButton", new Rect2(217, 18, 44, 12));
        CheckRect("EqualizerWindow/Equalizer/Controls/PreampSlider", new Rect2(21, 38, 14, 63));
        string[] bands = ["60", "170", "310", "600", "1K", "3K", "6K", "12K", "14K", "16K"];
        for (int i = 0; i < bands.Length; i++)
            CheckRect("EqualizerWindow/Equalizer/Controls/" + bands[i] + "Slider", new Rect2(78 + i * 18, 38, 14, 63));
    }

    /// <summary>Checks a scene control's local rectangle against a reference rectangle.</summary>
    /// <param name="path">Path beneath the application scene.</param>
    /// <param name="expected">Classic logical-pixel rectangle.</param>
    private void CheckRect(string path, Rect2 expected)
    {
        var control = _app.GetNode<Control>(path);
        Require(control.GetRect() == expected, $"{path}: expected {expected}, got {control.GetRect()}");
    }

    /// <summary>Checks that each scene connection updates its own EQ band and preamp effect.</summary>
    private void CheckAudioConnections()
    {
        int bus = AudioServer.GetBusIndex("Master");
        var effect = (AudioEffectEQ10)AudioServer.GetBusEffect(bus, AudioUtils.Eq10AudioEffectIndex);
        string[] bands = ["60", "170", "310", "600", "1K", "3K", "6K", "12K", "14K", "16K"];
        for (int i = 0; i < bands.Length; i++)
        {
            var slider = _app.GetNode<SkinSlider>("EqualizerWindow/Equalizer/Controls/" + bands[i] + "Slider");
            slider.Value = i + 1;
            Require(Mathf.IsEqualApprox(effect.GetBandGainDb(i), i + 1), $"EQ band {i}: scene signal binding");
            slider.Value = 0;
        }
        var preamp = _app.GetNode<SkinSlider>("EqualizerWindow/Equalizer/Controls/PreampSlider");
        preamp.Value = 3;
        var amplify = (AudioEffectAmplify)AudioServer.GetBusEffect(bus, AudioUtils.AmplifyAudioEffectIndex);
        Require(Mathf.IsEqualApprox(amplify.VolumeDb, 3), "Preamp scene signal binding");
        preamp.Value = 0;
    }

    /// <summary>Checks track frames and drags each slider to both endpoints through viewport input.</summary>
    /// <returns>A task completing after all interaction frames settle.</returns>
    private async Task CheckSliders()
    {
        var sliders = new[]
        {
            _mainControls.GetNode<SkinSlider>("VolumeSlider"),
            _mainControls.GetNode<SkinSlider>("PannerAudioSlider"),
            _mainControls.GetNode<SkinSlider>("PositionSeeker"),
            _equalizerControls.GetNode<SkinSlider>("PreampSlider"),
            _equalizerControls.GetNode<SkinSlider>("60Slider")
        };
        foreach (var slider in sliders)
        {
            _mainControls.Visible = slider.GetParent() == _mainControls;
            _equalizerControls.Visible = !_mainControls.Visible;
            slider.Editable = true;
            slider.Value = slider.MinValue;
            Vector2 expectedMinimum = slider.Layout == SkinSlider.SliderLayout.Equalizer ? new(1, 51) :
                slider.Layout == SkinSlider.SliderLayout.Seek ? Vector2.Zero : new(0, 1);
            Require(slider.ThumbRect.Position == expectedMinimum, $"{slider.Name}: minimum thumb");
            slider.Value = slider.MaxValue;
            Vector2 expectedMaximum = slider.Layout switch
            {
                SkinSlider.SliderLayout.Equalizer => new(1, 0),
                SkinSlider.SliderLayout.Seek => new(219, 0),
                SkinSlider.SliderLayout.Balance => new(24, 1),
                _ => new(51, 1)
            };
            Require(slider.ThumbRect.Position == expectedMaximum, $"{slider.Name}: maximum thumb");
            Rect2 expectedTrack = slider.Layout switch
            {
                SkinSlider.SliderLayout.Equalizer => new(208, 229, 14, 63),
                SkinSlider.SliderLayout.Seek => new(0, 0, 248, 10),
                SkinSlider.SliderLayout.Balance => new(9, 405, 38, 13),
                _ => new(0, 405, 68, 13)
            };
            Require(slider.TrackRegion == expectedTrack, $"{slider.Name}: maximum track");
            int starts = 0;
            int ends = 0;
            void Started() => starts++;
            void Ended(bool changed) { Require(changed, "Endpoint drag should change the value."); ends++; }
            slider.DragStarted += Started;
            slider.DragEnded += Ended;
            foreach (bool maximum in new[] { false, true })
            {
                Vector2 grab = slider.ThumbRect.GetCenter();
                await Move(slider, grab);
                Press(slider, grab, true);
                bool vertical = slider.Layout == SkinSlider.SliderLayout.Equalizer;
                Vector2 end = vertical ? new(7, maximum ? -20 : 100) : new(maximum ? 300 : -20, 6);
                await Move(slider, end, true);
                Press(slider, end, false);
                await Settle();
                Require(Mathf.IsEqualApprox(slider.Value, maximum ? slider.MaxValue : slider.MinValue),
                    $"{slider.Name}: viewport drag did not reach {(maximum ? "maximum" : "minimum")}, got {slider.Value}");
            }
            Require(starts == 2 && ends == 2, $"{slider.Name}: drag signal lifecycle");
            slider.DragStarted -= Started;
            slider.DragEnded -= Ended;
            slider.Value = (slider.MinValue + slider.MaxValue) / 2;
            if (slider.Layout == SkinSlider.SliderLayout.Equalizer)
                Require(slider.TrackRegion == new Rect2(208, 164, 14, 63) && slider.ThumbRect.Position == new Vector2(1, 26),
                    $"{slider.Name}: zero-gain frame and thumb");
            slider.Editable = false;
            double disabledValue = slider.Value;
            await Move(slider, slider.Size / 2);
            Press(slider, slider.Size / 2, true);
            Press(slider, slider.Size / 2, false);
            Require(Mathf.IsEqualApprox(slider.Value, disabledValue), $"{slider.Name}: disabled input");
            slider.Editable = true;
        }
    }

    /// <summary>Exercises released, hovered, held, latched, and disabled toggle artwork.</summary>
    /// <returns>A task completing after each state is rendered.</returns>
    private async Task CheckToggles()
    {
        string[] paths = ["MasterPanel/Controls/ShuffleModeButton", "MasterPanel/Controls/RepeatModeButton",
            "MasterPanel/Controls/ToggleEqualizerButton", "MasterPanel/Controls/TogglePlaylistButton",
            "EqualizerWindow/Equalizer/Controls/EqualizerToggleButton"];
        foreach (string path in paths)
        {
            bool equalizer = path.StartsWith("EqualizerWindow", StringComparison.Ordinal);
            _mainControls.Visible = !equalizer;
            _equalizerControls.Visible = equalizer;
            var button = (equalizer ? _equalizerControls : _mainControls).GetNode<SkinToggleButton>(path.Split('/')[^1]);
            for (int latched = 0; latched < 2; latched++)
            {
                button.SetPressedNoSignal(latched != 0);
                await Move(button, button.Size / 2);
                await Settle();
                var texture = (AtlasTexture)button.TextureNormal;
                Rect2 released = texture.Region;
                Rect2 expected = button.Name.ToString() switch
                {
                    "ShuffleModeButton" => new(28, latched * 30, 47, 15),
                    "RepeatModeButton" => new(0, latched * 30, 28, 15),
                    "ToggleEqualizerButton" => new(0, 61 + latched * 12, 23, 12),
                    "TogglePlaylistButton" => new(23, 61 + latched * 12, 23, 12),
                    _ => new(10 + latched * 59, 119, 25, 12)
                };
                Require(released == expected, $"{path}: released/hovered state {latched}");
                Press(button, button.Size / 2, true);
                await Settle();
                Require(texture.Region.Position == released.Position + button.HeldOffset,
                    $"{path}: held state {latched}, region {texture.Region}, expected offset {released.Position + button.HeldOffset}, hovered {button.IsHovered()}, draw {button.GetDrawMode()}");
                /* Cancel outside the hitbox, so testing window buttons cannot hide their windows. */
                await Move(button, new(-10, -10), true);
                await Settle();
                Require(texture.Region == released, $"{path}: cancelled held state");
                Press(button, new(-10, -10), false);
                button.Disabled = true;
                await Move(button, button.Size / 2);
                Press(button, button.Size / 2, true);
                await Settle();
                Require(texture.Region == released, $"{path}: disabled state");
                Press(button, button.Size / 2, false);
                button.Disabled = false;
            }
            button.SetPressedNoSignal(false);
            await Move(button, button.Size / 2);
            Press(button, button.Size / 2, true);
            Press(button, button.Size / 2, false);
            await Settle();
            Require(button.ButtonPressed, $"{path}: completed click should latch");
        }
    }

    /// <summary>Verifies titlebar focus transitions are reversible for every panel and selected skin.</summary>
    private void CheckTitlebars()
    {
        string[] paths = ["MasterPanel", "EqualizerWindow/Equalizer", "PlaylistWindow/Playlist", "VisualizerWindow/Visualizer"];
        foreach (string path in paths)
        {
            var panel = _app.GetNode<WindowPanelContainer>(path);
            panel.WindowRef.EmitSignal(Window.SignalName.FocusEntered);
            Rect2[] active = [.. panel.TitlebarTextures.Select(texture => texture.Region)];
            panel.WindowRef.EmitSignal(Window.SignalName.FocusExited);
            for (int i = 0; i < active.Length; i++)
                Require(panel.TitlebarTextures[i].Region.Position == active[i].Position + new Vector2(0, panel.InactiveTitlebarOffset),
                    $"{path}: inactive titlebar");
            panel.WindowRef.EmitSignal(Window.SignalName.FocusEntered);
            Require(panel.TitlebarTextures.Select(texture => texture.Region).SequenceEqual(active), $"{path}: restored titlebar");
        }
    }

    /// <summary>Routes mouse movement through the viewport using the control's canvas transform.</summary>
    /// <param name="control">Target control in its real application window.</param>
    /// <param name="position">Local logical coordinates.</param>
    /// <param name="held">Whether the left button is held.</param>
    /// <returns>A task completing after viewport input settles.</returns>
    private async Task Move(Control control, Vector2 position, bool held = false)
    {
        control.GetViewport().PushInput(new InputEventMouseMotion
        {
            Position = control.GetGlobalTransformWithCanvas() * position,
            ButtonMask = held ? MouseButtonMask.Left : 0
        }, true);
        await Settle();
    }

    /// <summary>Routes a mouse press or release through viewport hit testing.</summary>
    /// <param name="control">Target control in its real application window.</param>
    /// <param name="position">Local logical coordinates.</param>
    /// <param name="pressed">True for press, false for release.</param>
    private static void Press(Control control, Vector2 position, bool pressed)
    {
        control.GetViewport().PushInput(new InputEventMouseButton
        {
            Position = control.GetGlobalTransformWithCanvas() * position,
            ButtonIndex = MouseButton.Left,
            Pressed = pressed,
            ButtonMask = pressed ? MouseButtonMask.Left : 0
        }, true);
    }

    /// <summary>Waits for layout, input, and artwork updates.</summary>
    /// <returns>A task completing after the required process frames.</returns>
    private async Task Settle()
    {
        for (int i = 0; i < SettleFrames; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>Reports an actionable test failure.</summary>
    /// <param name="condition">Condition required for success.</param>
    /// <param name="message">Failure context.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
