using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GodAmp.Autoload;
using GodAmp.Controls.Playlist;
using GodAmp.Controls.MasterPanel;
using Godot;

namespace GodAmp.Tests;

/// <summary>Captures the application at supported zoom levels using an isolated settings directory.</summary>
public partial class ThemeCapture : Node
{
    private static readonly int[] Scales = [1, 2, 4];
    private const int LayoutSettleFrames = 4;
    /// <inheritdoc />
    public override void _Ready() => Callable.From(Run).CallDeferred();

    /// <summary>Checks startup restoration or skin switching, recording failures as a nonzero process exit.</summary>
    private async void Run()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(OS.GetEnvironment("GODAMP_DATA_DIR")))
                throw new InvalidOperationException("Set GODAMP_DATA_DIR to an isolated test directory.");
            string output = Path.Combine(SettingsManager.DataDirectory, "Captures");
            Directory.CreateDirectory(output);
            string expected = OS.GetEnvironment("GODAMP_EXPECT_SKIN");
            if (expected.Length > 0 && SkinLoader.GetCurrentSkinName() != expected)
                throw new InvalidOperationException($"Startup did not restore {expected}.");
            var app = GD.Load<PackedScene>("res://Src/Main.tscn").Instantiate();
            AddChild(app);
            app.GetNode<MasterPanel>("MasterPanel").ClockBlinkEverySeconds = float.MaxValue;
            app.GetNode<Timer>("MasterPanel/TextDisplay/MasterLabel/Timer").Stop();
            var entries = app.GetNode<Playlist>("PlaylistWindow/Playlist")
                .FindChildren("*", "PanelContainer", true, false).OfType<PlaylistTrackEntry>().ToArray();
            if (entries.Length < 2)
                throw new InvalidOperationException("Sample playlist is required for visual checks.");
            entries[1].IsSelected = true;

            string only = OS.GetEnvironment("GODAMP_CAPTURE_SKIN");
            if (expected.Length > 0)
            {
                await CaptureScales(app, output, "restart-" + Path.GetFileNameWithoutExtension(expected));
                CheckPlaylist(entries);
            }
            else
            {
                var cases = only.Length > 0 ? [only] : SkinLoader.GetAvailableSkins().Prepend("");
                foreach (string skin in cases)
                {
                    if (skin.Length == 0)
                        SkinLoader.RestoreOriginalSkin();
                    else if (!SkinLoader.Load(Path.Combine(SkinLoader.GetSkinsDirectory(), skin)))
                        throw new InvalidOperationException(SkinLoader.Instance.LastError);
                    await CaptureScales(app, output, skin.Length == 0 ? "default" : Path.GetFileNameWithoutExtension(skin));
                    CheckPlaylist(entries);
                }
                if (only.Length == 0)
                {
                    SkinLoader.RestoreOriginalSkin();
                    await CaptureScales(app, output, "restored-default");
                    CheckPlaylist(entries);
                }
            }
            GD.Print("PASS: rendered skins and live playlist styling");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PrintErr(error);
            GetTree().Quit(1);
        }
    }

    /// <summary>Asserts live updates to current-track text, normal text, selection, and fonts.</summary>
    /// <param name="entries">Sample rows with the first track playing and the second selected.</param>
    private static void CheckPlaylist(PlaylistTrackEntry[] entries)
    {
        var style = SkinLoader.Instance.PlaylistStyle;
        var current = entries[0].GetNode<Label>("HBoxContainer/TrackTitleLabel");
        var normal = entries[1].GetNode<Label>("HBoxContainer/TrackTitleLabel");
        var selection = entries[1].GetNode<ColorRect>("SelectedBg");
        if (current.GetThemeColor("font_color") != style.Current || normal.GetThemeColor("font_color") != style.Normal ||
            selection.Color != style.SelectedBackground || normal.GetThemeFont("font") != SkinLoader.Instance.PlaylistFont)
            throw new InvalidOperationException("Existing playlist entries did not update to the active skin.");
    }

    /// <summary>Captures a skin at each supported zoom level.</summary>
    /// <param name="app">Instantiated application scene.</param>
    /// <param name="output">Destination directory for PNG files.</param>
    /// <param name="name">Capture filename prefix.</param>
    /// <returns>A task completing after all PNGs have been saved.</returns>
    private async Task CaptureScales(Node app, string output, string name)
    {
        foreach (int scale in Scales)
            await Capture(app, output, name, scale);
    }

    /// <summary>Checks clock geometry and saves vertically stacked player, equalizer, and playlist windows.</summary>
    /// <param name="app">Instantiated application scene.</param>
    /// <param name="output">Destination directory for the PNG file.</param>
    /// <param name="name">Capture filename prefix.</param>
    /// <param name="scale">UI zoom multiplier.</param>
    /// <returns>A task completing when the rendered capture is saved.</returns>
    private async Task Capture(Node app, string output, string name, int scale)
    {
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.ZoomModeRequested, scale);
        for (int i = 0; i < LayoutSettleFrames; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        /* Native windows may stop scheduling redraws when another application covers them. */
        RenderingServer.ForceDraw(false);
        var digits = app.GetNode<Label>("MasterPanel/TextDisplay/TimeMinutesTensLabel");
        if (digits.GlobalPosition != new Vector2(48, 26) || digits.GetThemeFontSize("font_size") != 13)
            throw new InvalidOperationException("Clock digits must retain Winamp's logical coordinates and size at each scale.");
        var windows = new[] { GetTree().Root, app.GetNode<Window>("EqualizerWindow"), app.GetNode<Window>("PlaylistWindow") };
        var images = windows.Select(window => window.GetTexture().GetImage()).ToArray();
        var combined = Image.CreateEmpty(images.Max(image => image.GetWidth()), images.Sum(image => image.GetHeight()), false, Image.Format.Rgba8);
        int y = 0;
        foreach (Image image in images)
        {
            image.Convert(Image.Format.Rgba8);
            combined.BlitRect(image, new Rect2I(Vector2I.Zero, image.GetSize()), new Vector2I(0, y));
            y += image.GetHeight();
        }
        string path = Path.Combine(output, $"{name}-{scale}x.png");
        if (combined.SavePng(path) != Error.Ok)
            throw new IOException($"Could not save capture {path}.");
        GD.Print($"CAPTURE: {path}");
    }
}
