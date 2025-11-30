using Godot;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;

namespace GodAmp.Autoload;

public partial class SkinLoader : Node
{
    public static SkinLoader Instance { get; private set; }

    private const string SkinResourcesPath = "res://Data/SkinResources/";
    private const string TempExtractionFolder = "user://temp_skin/";
    private const string SkinsDirectoryName = "Skins";
    private const string BitmapFontPath = "res://Assets/Winamp/Raw/TEXT.png";

    private static readonly Dictionary<string, ImageTexture> LoadedTextures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Image> LoadedImages = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Texture2D> OriginalTextures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> AtlasToTextureName = new();

    private static string _skinsDirectory;
    private static string _currentSkinName;

    private static FontFile _bitmapFont;
    private static Image _originalFontImage;

    public override void _EnterTree()
    {
        if (Instance != null)
            QueueFree();
        Instance = this;

        InitializeSkinsDirectory();
        InitializeBitmapFont();
    }

    public override void _Ready()
    {
        LoadActiveSkin();
    }

    private static void InitializeSkinsDirectory()
    {
        string dataDir = OS.GetDataDir();
        string godampDir = Path.Combine(dataDir, "GodAmp");
        _skinsDirectory = Path.Combine(godampDir, SkinsDirectoryName);

        if (!Directory.Exists(_skinsDirectory))
        {
            Directory.CreateDirectory(_skinsDirectory);
            GD.Print($"Created skins directory: {_skinsDirectory}");
        }
    }

    private static void InitializeBitmapFont()
    {
        _bitmapFont = GD.Load<FontFile>(BitmapFontPath);
        _originalFontImage = _bitmapFont.GetTextureImage(0, Vector2I.Zero, 0);
    }

    private static void LoadActiveSkin()
    {
        string activeSkin = SettingsManager.Instance.GetActiveSkin();
        if (!string.IsNullOrEmpty(activeSkin))
        {
            string skinPath = Path.Combine(_skinsDirectory, activeSkin);
            if (File.Exists(skinPath))
            {
                GD.Print($"Loading active skin: {activeSkin}");
                Load(skinPath);
            }
            else
            {
                GD.Print($"Active skin not found: {activeSkin}, using default");
            }
        }
    }

    public static void Load(string filePath)
    {
        try
        {
            GD.Print($"Loading skin from: {filePath}");
            if (!File.Exists(filePath))
            {
                GD.PrintErr($"Skin file not found: {filePath}");
                return;
            }

            string tempPath = ExtractWszFile(filePath);
            if (string.IsNullOrEmpty(tempPath))
            {
                GD.PrintErr("Failed to extract .wsz file");
                return;
            }

            LoadTexturesFromPath(tempPath);
            ApplyTextureToAllAtlases();
            UpdateBitmapFont();
            CleanupTempFiles(tempPath);

            string fileName = Path.GetFileName(filePath);
            _currentSkinName = fileName;
            SettingsManager.Instance.SetActiveSkin(fileName);
            SettingsManager.Instance.SaveAllSettings();
            SignalBus.Instance.EmitSignal(SignalBus.SignalName.SkinChanged);
            GD.Print($"Skin '{fileName}' loaded successfully!");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error loading skin: {ex.Message}");
            GD.PrintErr($"Stack trace: {ex.StackTrace}");
        }
    }

    public static void RestoreOriginalSkin()
    {
        try
        {
            GD.Print("Restoring original skin...");

            int restoredCount = GetAllAtlasResourcePaths().Count(TryRestoreAtlasTexture);
            RestoreBitmapFont();

            GD.Print($"Restored {restoredCount} atlas textures to original skin");
            LoadedTextures.Clear();
            _currentSkinName = null;

            SettingsManager.Instance.SetActiveSkin("");
            SettingsManager.Instance.SaveAllSettings();
            SignalBus.Instance.EmitSignal(SignalBus.SignalName.SkinChanged);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error restoring original skin: {ex.Message}");
        }
    }

    private static void RestoreBitmapFont()
    {
        _bitmapFont.SetTextureImage(0, Vector2I.Zero, 0, _originalFontImage);
        GD.Print("Restored original bitmap font texture");
    }

    private static bool TryRestoreAtlasTexture(string resourcePath)
    {
        var atlasTexture = GD.Load<AtlasTexture>(resourcePath);
        if (atlasTexture?.Atlas == null)
            return false;

        if (!AtlasToTextureName.TryGetValue(resourcePath, out var textureName))
            return false;

        if (!OriginalTextures.TryGetValue(textureName, out var texture))
            return false;

        atlasTexture.Atlas = texture;
        return true;
    }

    public static string GetCurrentSkinName()
    {
        return _currentSkinName;
    }

    public static string GetSkinsDirectory()
    {
        return _skinsDirectory;
    }

    public static string[] GetAvailableSkins()
    {
        if (string.IsNullOrEmpty(_skinsDirectory) || !Directory.Exists(_skinsDirectory))
            return [];

        return Directory.GetFiles(_skinsDirectory, "*.wsz")
            .Select(Path.GetFileName)
            .ToArray();
    }

    private static string ExtractWszFile(string filePath)
    {
        try
        {
            // Convert Godot path to system path
            string systemPath = ProjectSettings.GlobalizePath(filePath);

            if (!File.Exists(systemPath))
            {
                GD.PrintErr($"File not found: {systemPath}");
                return null;
            }

            // Extract zip file
            string tempPath = ProjectSettings.GlobalizePath(TempExtractionFolder);
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
            Directory.CreateDirectory(tempPath);

            ZipFile.ExtractToDirectory(systemPath, tempPath);
            GD.Print($"Extracted skin to: {tempPath}");

            return tempPath;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error extracting .wsz file: {ex.Message}");
            return null;
        }
    }

    private static void LoadTexturesFromPath(string tempPath)
    {
        LoadedTextures.Clear();
        LoadedImages.Clear();

        try
        {
            var imageFiles = Directory.GetFiles(tempPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.ToLower().EndsWith(".png") ||
                           f.ToLower().EndsWith(".bmp") ||
                           f.ToLower().EndsWith(".jpg") ||
                           f.ToLower().EndsWith(".jpeg"))
                .ToArray();

            GD.Print($"Found {imageFiles.Length} image files in skin");

            foreach (var imagePath in imageFiles)
            {
                try
                {
                    var image = Image.LoadFromFile(imagePath);
                    if (image != null)
                    {
                        var texture = ImageTexture.CreateFromImage(image);
                        string fileName = Path.GetFileName(imagePath).ToUpper();
                        LoadedTextures[fileName] = texture;
                        LoadedImages[fileName] = image;
                        GD.Print($"Loaded texture: {fileName}");
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Failed to load image {imagePath}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error loading textures from path: {ex.Message}");
        }
    }

    private static List<string> GetAllAtlasResourcePaths()
    {
        var resources = new List<string>();

        try
        {
            var dir = DirAccess.Open(SkinResourcesPath);
            if (dir == null)
            {
                GD.PrintErr($"Failed to open directory: {SkinResourcesPath}");
                return resources;
            }

            dir.ListDirBegin();
            string fileName = dir.GetNext();

            while (fileName != "")
            {
                if (!dir.CurrentIsDir() && fileName.EndsWith(".tres"))
                    resources.Add(SkinResourcesPath + fileName);

                fileName = dir.GetNext();
            }
            dir.ListDirEnd();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error getting atlas resources: {ex.Message}");
        }

        return resources;
    }

    private static void ApplyTextureToAllAtlases()
    {
        var atlasResourcePaths = GetAllAtlasResourcePaths();
        GD.Print($"Found {atlasResourcePaths.Count} atlas texture resources");

        int replacedCount = atlasResourcePaths.Count(UpdateAtlasTexture);

        GD.Print($"Successfully updated {replacedCount} atlas textures");
    }

    private static void UpdateBitmapFont()
    {
        string matchingKey = FindMatchingImageKey("TEXT.PNG");
        if (matchingKey == null)
        {
            GD.Print("No TEXT.png found in skin, keeping default font");
            return;
        }

        _bitmapFont.SetTextureImage(0, Vector2I.Zero, 0, LoadedImages[matchingKey]);
        GD.Print("Updated bitmap font texture");
    }

    private static string FindMatchingImageKey(string imageName)
    {
        string baseNameWithoutExt = Path.GetFileNameWithoutExtension(imageName);
        return LoadedImages.Keys.FirstOrDefault(k =>
            Path.GetFileNameWithoutExtension(k).Equals(baseNameWithoutExt, StringComparison.OrdinalIgnoreCase));
    }

    private static bool UpdateAtlasTexture(string resourcePath)
    {
        try
        {
            var atlasTexture = GD.Load<AtlasTexture>(resourcePath);
            if (atlasTexture?.Atlas == null)
            {
                GD.PrintErr($"Failed to load atlas or missing base texture: {resourcePath}");
                return false;
            }

            string textureName = GetOrStoreTextureName(resourcePath, atlasTexture);
            if (string.IsNullOrEmpty(textureName))
            {
                GD.PrintErr($"Could not determine texture name for: {resourcePath}");
                return false;
            }

            string matchingKey = FindMatchingTextureKey(textureName);
            if (matchingKey == null)
            {
                GD.Print($"No matching texture found for: {textureName}");
                return false;
            }

            atlasTexture.Atlas = LoadedTextures[matchingKey];
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error updating atlas texture {resourcePath}: {ex.Message}");
            return false;
        }
    }

    private static string GetOrStoreTextureName(string resourcePath, AtlasTexture atlasTexture)
    {
        if (AtlasToTextureName.TryGetValue(resourcePath, out var textureName))
            return textureName;

        textureName = GetTextureNameFromPath(atlasTexture.Atlas.ResourcePath);
        if (string.IsNullOrEmpty(textureName))
            return null;

        AtlasToTextureName[resourcePath] = textureName;
        if (!OriginalTextures.ContainsKey(textureName))
        {
            OriginalTextures[textureName] = atlasTexture.Atlas;
            GD.Print($"Stored original texture: {textureName}");
        }

        return textureName;
    }

    private static string FindMatchingTextureKey(string textureName)
    {
        string baseNameWithoutExt = Path.GetFileNameWithoutExtension(textureName);
        return LoadedTextures.Keys.FirstOrDefault(k =>
            Path.GetFileNameWithoutExtension(k).Equals(baseNameWithoutExt, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetTextureNameFromPath(string texturePath)
    {
        return Path.GetFileName(texturePath).ToUpper(); // extract just the filename from paths like "res://Assets/Winamp/Raw/CBUTTONS.png"
    }

    private static void CleanupTempFiles(string tempPath)
    {
        try
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
                GD.Print("Cleaned up temporary files");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error cleaning up temp files: {ex.Message}");
        }
    }
}
