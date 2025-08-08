using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GodAmp.Data;
using Godot;
using FileAccess = Godot.FileAccess;
using TagLib;
using File = System.IO.File;

namespace GodAmp.Utils;

public static class AudioUtils
{
    private static readonly Dictionary<string, Func<byte[], AudioStream>> StreamFactories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { ".mp3", bytes => new AudioStreamMP3 { Data = bytes } },
            { ".wav", bytes => AudioStreamWav.LoadFromBuffer(bytes) },
            { ".ogg", AudioStreamOggVorbis.LoadFromBuffer },
        };

    public const int SpectrumAnalyzerAudioEffectIndex = 0;
    public const int AmplifyAudioEffectIndex = 1;
    public const int Eq10AudioEffectIndex = 2;
    public const int PannerAudioEffectIndex = 3;

    public static List<Track> LoadAllTracksFromDir(string directoryPath)
    {
        var result = new List<Track>();
        var dir = DirAccess.Open(directoryPath);
        if (dir == null)
        {
            GD.PrintErr("Directory not found: " + directoryPath);
            return result;
        }

        dir.ListDirBegin();
        var fileName = dir.GetNext().Replace(".import", "");

        while (!string.IsNullOrEmpty(fileName))
        {
            GD.Print("Checking: ", fileName);
            if (!dir.CurrentIsDir() && fileName.EndsWith(".mp3"))
            {
                var fullPath = Path.Combine(directoryPath, fileName);
                var track = LoadTrack(fullPath);
                if (track != null)
                {
                    result.Add(track);
                }
            }

            fileName = dir.GetNext();
        }

        dir.ListDirEnd();
        return result;
    }

    // Overload that figures out the factory by extension and loads/creates the stream
    public static Track LoadTrack(string fullPath)
    {
        try
        {
            var ext = Path.GetExtension(fullPath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !StreamFactories.TryGetValue(ext, out var factory))
            {
                GD.PrintErr("Unsupported audio extension for file: " + fullPath);
                return null;
            }

            var file = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr("Failed to open file: " + fullPath);
                return null;
            }

            var data = file.GetBuffer((long)file.GetLength());
            file.Close();

            AudioStream stream = factory(data);
            
            // TagLib needs a real file; write to temp, read tags, then clean up
            var tempDir = Path.GetTempPath();
            var tempFile = Path.Combine(tempDir, Path.GetFileName(fullPath));
            File.WriteAllBytes(tempFile, data);

            var tagFile = TagLib.File.Create(tempFile);
            var tag = tagFile.Tag;
            var props = tagFile.Properties;

            var fileNameNoExt = Path.GetFileNameWithoutExtension(fullPath);
            var useFileName = string.IsNullOrWhiteSpace(tag.Title);

            var t = new Track
            {
                Name = useFileName ? fileNameNoExt : tag.Title,
                Artist = tag.FirstPerformer ?? "Unknown",
                Album = tag.Album,
                TrackNumber = (int)tag.Track,
                Duration = (float)props.Duration.TotalSeconds,
                BitrateKbps = props.AudioBitrate,
                SampleRateHz = props.AudioSampleRate,
                Stream = stream,
                UseFileName = useFileName,
            };

            try { File.Delete(tempFile); } catch { /* ignore */ }

            GD.Print("Loaded track: " + t.Name);
            return t;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error loading track {fullPath}: {e}");
            return null;
        }
    }

    public static List<Track> LoadTracksFromPathList(string[] pathList)
    {
        return pathList.Where(p => !string.IsNullOrWhiteSpace(p))
                       .Select(LoadTrack)
                       .Where(t => t != null)
                       .ToList();
    }

    public static string GetFullTrackTitle(Track track)
    {
        if (track == null)
            return "Unknown Track";
        return track.UseFileName
            ? track.Name
            : $"{track.TrackNumber}. {track.Artist} - {track.Name}";
    }

    public static List<string> GetAllowedFileFilters()
    {
        return StreamFactories.Keys.Select(k => $"*{k.ToLowerInvariant()}").ToList();
    }
}
