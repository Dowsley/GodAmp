using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using GodAmp.Autoload;
using GodAmp.Data;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Tests;

/// <summary>Exercises classic skin parsing and state transitions with generated archive fixtures.</summary>
public partial class ThemeTests : Node
{
    private string _fixtures = "";

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(OS.GetEnvironment("GODAMP_DATA_DIR")))
                throw new InvalidOperationException("Set GODAMP_DATA_DIR to an isolated test directory.");
            _fixtures = Path.Combine(SettingsManager.DataDirectory, "Fixtures");
            Directory.CreateDirectory(_fixtures);
            TestMetadata();
            TestGlyphs();
            TestAssetResolution();
            TestSwitching();
            TestInstalledSkins();
            GD.Print("PASS: theme regression tests");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PrintErr(error);
            GetTree().Quit(1);
        }
    }

    /// <summary>Fails the test run when an expected behavior is absent.</summary>
    /// <param name="condition">Whether the tested behavior holds.</param>
    /// <param name="message">Failure description identifying the violated contract.</param>
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>Creates uniformly colored fixture artwork.</summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="color">Color used to distinguish asset sources.</param>
    /// <returns>An RGBA image of the requested size and color.</returns>
    private static Image Solid(int width, int height, Color color)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(color);
        return image;
    }

    /// <summary>Writes a skin archive in the isolated fixture directory.</summary>
    /// <param name="name">Archive filename without its extension.</param>
    /// <param name="files">Entry paths and uncompressed contents.</param>
    /// <returns>The absolute path to the generated WSZ file.</returns>
    private string Archive(string name, params (string Name, byte[] Bytes)[] files)
    {
        string path = Path.Combine(_fixtures, name + ".wsz");
        using var output = File.Create(path);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (var file in files)
        {
            using var entry = archive.CreateEntry(file.Name).Open();
            entry.Write(file.Bytes);
        }
        return path;
    }

    /// <summary>Checks playlist section parsing, per-field defaults, and installed-font precedence.</summary>
    private static void TestMetadata()
    {
        var style = PlaylistSkinStyle.Parse("\uFEFF[other]\nNormal=#ffffff\n[tExT]\nNormal=#C80000\nCurrent=FF0000\nNormalBG=#150000 ; comment\nSelectedBG=broken\nFont=Tahoma\n");
        Check(style.Normal == new Color("c80000") && style.Current == Colors.Red && style.Background == new Color("150000"), "Playlist colors must parse case-insensitively in the Text section.");
        Check(style.SelectedBackground == PlaylistSkinStyle.Default.SelectedBackground, "Malformed colors must use field defaults.");
        Check(style.FontName == "Tahoma", "Playlist font name was not parsed.");
        Check(style.ResolveFontName(["Arial", "TAHOMA"]) == "TAHOMA", "Requested installed font must win.");
        Check(style.ResolveFontName(["Arial"]) == "Arial", "Unavailable fonts must fall back predictably.");
        Check(style.ResolveFontName([]) == "", "No installed match must use the bundled font fallback.");
        Check(PlaylistSkinStyle.Parse("[Text]\nFont=\nNormalBG=#garbage") == PlaylistSkinStyle.Default, "Missing and malformed metadata must preserve defaults.");
    }

    /// <summary>Checks glyph coordinates, aliases, advances, and preserved source colors.</summary>
    private static void TestGlyphs()
    {
        var font = SkinBitmapFont.CreateText(Solid(156, 74, Colors.Blue));
        Check(font.GetGlyphUVRect(0, new Vector2I(6, 0), '0') == new Rect2(0, 6, 5, 6), "Text glyph cells must not stretch with sheet dimensions.");
        Check(font.GetGlyphUVRect(0, new Vector2I(6, 0), '?') == new Rect2(15, 12, 5, 6), "Third-row glyph mapping is incorrect.");
        Check(font.GetGlyphUVRect(0, new Vector2I(6, 0), '~') == new Rect2(120, 6, 5, 6), "Winamp punctuation aliases must resolve correctly.");
        var numbers = SkinBitmapFont.CreateNumbers(Solid(108, 13, Colors.Red), true);
        Check(numbers.GetGlyphUVRect(0, new Vector2I(13, 0), '-') == new Rect2(99, 0, 9, 13), "Extended minus glyph must use the last cell.");
        Check(numbers.GetGlyphAdvance(0, 13, '8') == new Vector2(9, 0), "Clock digits must advance exactly nine pixels.");
        Check(numbers.GetTextureImage(0, new Vector2I(13, 0), 0).GetPixel(0, 0) == Colors.Red, "Colored bitmap backgrounds must survive font creation.");
    }

    /// <summary>Checks deterministic duplicate selection and default-image resolution.</summary>
    private void TestAssetResolution()
    {
        string path = Archive("duplicates",
            ("nested/MAIN.bmp", Solid(275, 116, Colors.Red).SavePngToBuffer()),
            ("MAIN.PNG", Solid(275, 116, Colors.Blue).SavePngToBuffer()));
        var archive = SkinArchive.Read(path);
        var fallback = Solid(275, 116, Colors.Green);
        Image resolved = archive.ResolveImage("main", fallback);
        Check(resolved.GetPixel(0, 0) == Colors.Blue, "Root artwork must take precedence over nested duplicates.");
        Check(ReferenceEquals(resolved, archive.Images["MAIN"]), "Full-size artwork should not allocate a padded copy.");
        Check(ReferenceEquals(archive.ResolveImage("GEN", fallback), fallback), "Missing artwork must retain the supplied fallback.");
    }

    /// <summary>Checks missing-asset fallback, rejected-load isolation, and complete default restoration.</summary>
    private void TestSwitching()
    {
        SkinLoader.RestoreOriginalSkin();
        var title = GD.Load<AtlasTexture>("res://Data/SkinResources/VisualizerTitlebar.tres");
        var volume = GD.Load<AtlasTexture>("res://Data/SkinResources/VolumeSlider.tres");
        var balance = GD.Load<AtlasTexture>("res://Data/SkinResources/PannerAudioSlider.tres");
        Texture2D originalTitle = title.Atlas;
        FontFile originalNumbers = SkinLoader.Instance.NumberFont;
        string full = Archive("full",
            ("nested\\GEN.PNG", Solid(194, 109, Colors.Red).SavePngToBuffer()),
            ("VOLUME.PNG", Solid(68, 418, Colors.Blue).SavePngToBuffer()),
            ("NUMBERS.PNG", Solid(99, 13, Colors.Green).SavePngToBuffer()),
            ("NUMS_EX.PNG", Solid(108, 13, Colors.Red).SavePngToBuffer()),
            ("PLEDIT.TXT", Encoding.UTF8.GetBytes("[Text]\nNormal=#112233\nFont=Missing Test Font")));
        Check(SkinLoader.Load(full), SkinLoader.Instance.LastError ?? "Full fixture failed to load.");
        Check(title.Atlas.GetImage().GetPixel(0, 0) == Colors.Red, "Nested Windows asset paths must load.");
        Check(balance.Atlas.GetImage().GetPixel(9, 0) == Colors.Blue, "Missing balance must use the custom volume sheet.");
        Check(volume.Atlas.GetImage().GetHeight() >= 433 && volume.Atlas.GetImage().GetPixel(15, 422).A == 0, "Cropped slider sheets must leave omitted thumb pixels transparent.");
        Check(SkinLoader.Instance.NumberFont.GetTextureImage(0, new Vector2I(13, 0), 0).GetPixel(0, 0) == Colors.Red, "NUMS_EX must take precedence over NUMBERS.");

        string partial = Archive("partial", ("MAIN.PNG", Solid(275, 116, Colors.Blue).SavePngToBuffer()));
        Check(SkinLoader.Load(partial), "Partial fixture failed to load.");
        Check(title.Atlas == originalTitle, "Missing GEN must restore the built-in texture, not retain the previous skin.");
        Check(SkinLoader.Instance.PlaylistStyle == PlaylistSkinStyle.Default, "Missing metadata must reset playlist styling.");
        Check(SkinLoader.Instance.NumberFont.GetTextureImage(0, new Vector2I(13, 0), 0).GetData().SequenceEqual(originalNumbers.GetTextureImage(0, new Vector2I(13, 0), 0).GetData()), "Missing number sheets must reset digits.");
        var activeFont = SkinLoader.Instance.NumberFont;
        var main = GD.Load<AtlasTexture>("res://Data/SkinResources/MasterPanelBackground.tres");
        var activeMain = main.Atlas;
        string activeName = SettingsManager.Instance.GetActiveSkin();
        byte[] savedSettings = File.ReadAllBytes(Path.Combine(SettingsManager.DataDirectory, "godamp.ini"));
        int signals = 0;
        void Changed() => signals++;
        SignalBus.Instance.SkinChanged += Changed;
        string invalid = Archive("invalid", ("MAIN.PNG", [1, 2, 3]));
        Check(!SkinLoader.Load(invalid), "Corrupt images must fail loading.");
        string undersized = Archive("undersized", ("MAIN.PNG", Solid(2, 2, Colors.Red).SavePngToBuffer()));
        Check(!SkinLoader.Load(undersized), "Undersized required artwork must fail loading.");
        Check(!SkinLoader.Load(Archive("empty", ("readme.txt", Encoding.UTF8.GetBytes("not a skin")))), "Archives without skin images must fail loading.");
        string corruptArchive = Path.Combine(_fixtures, "broken.wsz");
        File.WriteAllText(corruptArchive, "not a ZIP archive");
        Check(!SkinLoader.Load(corruptArchive), "Invalid archives must fail loading.");
        Check(!SkinLoader.Load(Path.Combine(_fixtures, "missing.wsz")), "Missing archives must fail loading.");
        Check(SkinLoader.Instance.NumberFont == activeFont && main.Atlas == activeMain && SettingsManager.Instance.GetActiveSkin() == activeName && signals == 0, "A rejected skin must not change fonts, artwork, saved settings, or emit a skin change.");
        Check(File.ReadAllBytes(Path.Combine(SettingsManager.DataDirectory, "godamp.ini")).SequenceEqual(savedSettings), "Failed loads must leave persisted settings unchanged.");
        SignalBus.Instance.SkinChanged -= Changed;
        SkinLoader.RestoreOriginalSkin();
        Check(title.Atlas == originalTitle && SkinLoader.Instance.NumberFont == originalNumbers && SettingsManager.Instance.GetActiveSkin() == "", "Default restoration must reset textures, fonts, and saved selection.");
        Check(SkinLoader.Load(partial), "Partial fixture must also load from defaults.");
        Check(main.Atlas.GetImage().GetPixel(0, 0) == Colors.Blue && title.Atlas == originalTitle, "Partial-skin output must be independent of its predecessor.");
        SkinLoader.RestoreOriginalSkin();
        Check(!Directory.Exists(Path.Combine(SettingsManager.DataDirectory, "temp_skin")), "Skin loading must not leave extracted files.");
    }

    /// <summary>Optionally checks archives supplied by GODAMP_TEST_SKINS without modifying their source files.</summary>
    private static void TestInstalledSkins()
    {
        string source = OS.GetEnvironment("GODAMP_TEST_SKINS");
        if (string.IsNullOrEmpty(source))
            return;
        foreach (string path in Directory.GetFiles(source, "*.wsz").Order(StringComparer.Ordinal))
        {
            Check(SkinLoader.Load(path), SkinLoader.Instance.LastError ?? path);
            var archive = SkinArchive.Read(path);
            Check(SkinLoader.Instance.PlaylistStyle == archive.PlaylistStyle, $"Metadata not applied for {path}.");
            GD.Print($"PASS: {Path.GetFileName(path)}");
        }
        SkinLoader.RestoreOriginalSkin();
    }
}
