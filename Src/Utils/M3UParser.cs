using System;
using System.Collections.Generic;
using System.IO;

namespace GodAmp.Utils;

public static class M3UParser
{
    public static string[] Parse(string filePath)
    {
        var result = new List<string>();
        var baseDir = Path.GetDirectoryName(filePath) ?? string.Empty;
        foreach (var raw in File.ReadLines(filePath))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line))
                continue;
            if (line.StartsWith("#"))
                continue;
            string path = line;
            if (!Path.IsPathRooted(path))
                path = Path.GetFullPath(Path.Combine(baseDir, path));
            result.Add(path);
        }
        return result.ToArray();
    }

    public static void Write(string filePath, IEnumerable<string> absolutePaths, bool relativePaths = true)
    {
        var baseDir = Path.GetDirectoryName(filePath) ?? string.Empty;
        using var writer = new StreamWriter(filePath, false);
        writer.WriteLine("#EXTM3U");
        foreach (var abs in absolutePaths)
        {
            var line = relativePaths ? MakeRelativePath(baseDir, abs) : abs;
            writer.WriteLine(line);
        }
    }

    private static string MakeRelativePath(string baseDir, string targetPath)
    {
        try
        {
            var baseUri = new Uri(AppendDirectorySeparatorChar(baseDir));
            var targetUri = new Uri(targetPath);
            var rel = baseUri.MakeRelativeUri(targetUri).ToString();
            return Uri.UnescapeDataString(rel).Replace('/', Path.DirectorySeparatorChar);
        }
        catch
        {
            return targetPath;
        }
    }

    private static string AppendDirectorySeparatorChar(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar))
            return path + Path.DirectorySeparatorChar;
        return path;
    }
}


