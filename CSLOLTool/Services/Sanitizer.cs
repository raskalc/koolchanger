using System.Text.RegularExpressions;

namespace CSLOLTool.Services;

// made by random guy from lolru discord
public static class Sanitizer
{
    private static string CleanBase(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";

        var cleaned = Regex.Replace(s, @"[^A-Za-z0-9_-]+", "_");

        cleaned = Regex.Replace(cleaned, @"_+", "_");

        cleaned = cleaned.Trim('_', '-');

        return cleaned;
    }

    private static string EnsureUniquePath(string targetPath)
    {
        if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
            return targetPath;

        var dir = Path.GetDirectoryName(targetPath)!;
        var name = Path.GetFileName(targetPath);
        var baseName = name;
        string? ext = null;

        if (File.Exists(targetPath))
        {
            ext = Path.GetExtension(name);
            baseName = Path.GetFileNameWithoutExtension(name);
        }

        var i = 1;
        while (true)
        {
            var candidate = ext is null
                ? Path.Combine(dir, $"{baseName}-{i}")
                : Path.Combine(dir, $"{baseName}-{i}{ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
            i++;
        }
    }

    public static void SanitizeTree(string root)
    {
        if (!Directory.Exists(root))
            return;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            try
            {
                var dir = Path.GetDirectoryName(file)!;
                var ext = Path.GetExtension(file);
                var cleanBase = CleanBase(Path.GetFileNameWithoutExtension(file));
                var newPath = Path.Combine(dir, cleanBase + ext);

                if (!string.Equals(file, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    newPath = EnsureUniquePath(newPath);
                    File.Move(file, newPath);
                }
            }
            catch (Exception)
            {
            }

        var allDirs = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories);
        var dirsSorted = new List<string>(allDirs);
        dirsSorted.Sort((a, b) => b.Length.CompareTo(a.Length));

        foreach (var dirPath in dirsSorted)
            try
            {
                var parent = Path.GetDirectoryName(dirPath)!;
                var clean = CleanBase(Path.GetFileName(dirPath));
                if (string.IsNullOrEmpty(clean)) clean = "folder";

                var newPath = Path.Combine(parent, clean);

                if (!string.Equals(dirPath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    newPath = EnsureUniquePath(newPath);
                    Directory.Move(dirPath, newPath);
                }
            }
            catch (Exception)
            {
            }

        try
        {
            var parent = Path.GetDirectoryName(root);
            if (!string.IsNullOrEmpty(parent))
            {
                var clean = CleanBase(Path.GetFileName(root));
                if (string.IsNullOrEmpty(clean)) clean = "root";
                var newRoot = Path.Combine(parent, clean);
                if (!string.Equals(root, newRoot, StringComparison.OrdinalIgnoreCase))
                {
                    var unique = EnsureUniquePath(newRoot);
                    Directory.Move(root, unique);
                }
            }
        }
        catch (Exception)
        {
        }
    }
}