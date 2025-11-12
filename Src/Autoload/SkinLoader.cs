using Godot;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace GodAmp.Autoload;

public partial class SkinLoader : Node
{
    public static SkinLoader Instance { get; private set; }

    private const string SkinResourcesPath = "res://Data/SkinResources/";
    private const string TempExtractionFolder = "user://temp_skin/";
    private const string SkinsDirectoryName = "Skins";

    private static readonly Dictionary<string, ImageTexture> LoadedTextures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Texture2D> OriginalTextures = new(StringComparer.OrdinalIgnoreCase);

    private static string _skinsDirectory;
    private static string _currentSkinName;

    public override void _EnterTree()
    {
        if (Instance != null)
            QueueFree();
        Instance = this;

        InitializeSkinsDirectory();
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

    private static void LoadActiveSkin()
    {
        string activeSkin = SettingsManager.Instance.GetActiveSkin();
        if (!string.IsNullOrEmpty(activeSkin))
        {
            string skinPath = Path.Combine(_skinsDirectory, activeSkin);
            if (File.Exists(skinPath))
            {
                GD.Print($"Loading active skin: {activeSkin}");
                LoadFromSkinsFolder(activeSkin);
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

            string fileName = Path.GetFileName(filePath);
            string targetPath = Path.Combine(_skinsDirectory, fileName);

            if (!File.Exists(targetPath))
            {
                GD.Print($"Copying skin to: {targetPath}");
                File.Copy(filePath, targetPath, false);
            }
            else
            {
                GD.Print($"Skin already exists in Skins folder");
            }

            LoadFromSkinsFolder(fileName);
            SettingsManager.Instance.SetActiveSkin(fileName);
            SettingsManager.Instance.SaveAllSettings();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error loading skin: {ex.Message}");
            GD.PrintErr($"Stack trace: {ex.StackTrace}");
        }
    }

    private static void LoadFromSkinsFolder(string skinFileName)
    {
        try
        {
            string skinPath = Path.Combine(_skinsDirectory, skinFileName);

            if (!File.Exists(skinPath))
            {
                GD.PrintErr($"Skin not found in Skins folder: {skinFileName}");
                return;
            }

            string tempPath = ExtractWszFile(skinPath);
            if (string.IsNullOrEmpty(tempPath))
            {
                GD.PrintErr("Failed to extract .wsz file");
                return;
            }

            LoadTexturesFromPath(tempPath);
            ApplyTextureToAllAtlases();
            CleanupTempFiles(tempPath);

            _currentSkinName = skinFileName;
            GD.Print($"Skin '{skinFileName}' loaded successfully!");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error loading skin from Skins folder: {ex.Message}");
            GD.PrintErr($"Stack trace: {ex.StackTrace}");
        }
    }

    public static void RestoreOriginalSkin()
    {
        try
        {
            GD.Print("Restoring original skin...");

            var atlasResourcePaths = GetAllAtlasResourcePaths();
            int restoredCount = 0;

            foreach (var resourcePath in atlasResourcePaths)
            {
                var atlasTexture = GD.Load<AtlasTexture>(resourcePath);
                if (atlasTexture is { Atlas: not null })
                {
                    string textureName = GetTextureNameFromPath(atlasTexture.Atlas.ResourcePath);
                    if (OriginalTextures.TryGetValue(textureName, out var texture))
                    {
                        atlasTexture.Atlas = texture;
                        restoredCount++;
                    }
                }
            }

            GD.Print($"Restored {restoredCount} atlas textures to original skin");
            LoadedTextures.Clear();
            _currentSkinName = null;

            // Clear active skin from settings
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetActiveSkin("");
                SettingsManager.Instance.SaveAllSettings();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error restoring original skin: {ex.Message}");
        }
    }

    public static string GetCurrentSkinName()
    {
        return _currentSkinName;
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

            // Create temp directory
            string tempPath = ProjectSettings.GlobalizePath(TempExtractionFolder);
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
            Directory.CreateDirectory(tempPath);

            // Extract zip file
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
            if (dir != null)
            {
                dir.ListDirBegin();
                string fileName = dir.GetNext();

                while (fileName != "")
                {
                    if (!dir.CurrentIsDir() && fileName.EndsWith(".tres"))
                    {
                        resources.Add(SkinResourcesPath + fileName);
                    }
                    fileName = dir.GetNext();
                }
                dir.ListDirEnd();
            }
            else
            {
                GD.PrintErr($"Failed to open directory: {SkinResourcesPath}");
            }
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

        int replacedCount = 0;
        foreach (var resourcePath in atlasResourcePaths)
        {
            if (UpdateAtlasTexture(resourcePath))
            {
                replacedCount++;
            }
        }

        GD.Print($"Successfully updated {replacedCount} atlas textures");
    }

    private static void RestoreAllAtlasesToOriginal()
    {
        var atlasResourcePaths = GetAllAtlasResourcePaths();
        int restoredCount = 0;

        foreach (var resourcePath in atlasResourcePaths)
        {
            var atlasTexture = GD.Load<AtlasTexture>(resourcePath);
            if (atlasTexture is { Atlas: not null })
            {
                string textureName = GetTextureNameFromPath(atlasTexture.Atlas.ResourcePath);
                if (OriginalTextures.TryGetValue(textureName, out var texture))
                {
                    atlasTexture.Atlas = texture;
                    restoredCount++;
                }
            }
        }

        GD.Print($"Restored {restoredCount} atlas textures to original skin");
    }

    private static bool UpdateAtlasTexture(string resourcePath)
    {
        try
        {
            // Load the atlas texture resource
            var atlasTexture = GD.Load<AtlasTexture>(resourcePath);
            if (atlasTexture == null)
            {
                GD.PrintErr($"Failed to load atlas texture: {resourcePath}");
                return false;
            }

            // Get the current texture name
            if (atlasTexture.Atlas == null)
            {
                GD.PrintErr($"Atlas texture has no base texture: {resourcePath}");
                return false;
            }

            // Store the original texture if we haven't already
            string textureName = GetTextureNameFromPath(atlasTexture.Atlas.ResourcePath);
            if (!OriginalTextures.ContainsKey(textureName))
            {
                OriginalTextures[textureName] = atlasTexture.Atlas;
                GD.Print($"Stored original texture: {textureName}");
            }

            // Find matching texture in loaded textures (case-insensitive, extension-insensitive)
            string baseNameWithoutExt = Path.GetFileNameWithoutExtension(textureName);
            string matchingKey = LoadedTextures.Keys.FirstOrDefault(k =>
                Path.GetFileNameWithoutExtension(k).Equals(baseNameWithoutExt, StringComparison.OrdinalIgnoreCase));

            if (matchingKey != null)
            {
                atlasTexture.Atlas = LoadedTextures[matchingKey];
                GD.Print($"Updated atlas texture: {Path.GetFileName(resourcePath)} with {matchingKey}");
                return true;
            }

            GD.Print($"No matching texture found for: {textureName}");
            return false;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error updating atlas texture {resourcePath}: {ex.Message}");
            return false;
        }
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
