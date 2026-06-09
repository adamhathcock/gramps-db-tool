using GrampsDbTool.Configuration;

namespace GrampsDbTool.Services;

public interface IMediaPathService
{
    string ResolvePath(string storedPath);
    string ToRelativePath(string absolutePath);
    bool IsInsideMediaRoot(string path);
    void ValidateMediaPath(string path);
}

public sealed class MediaPathService(GrampsDatabasePaths databasePaths) : IMediaPathService
{
    private readonly string mediaRoot = NormalizeDirectory(databasePaths.MediaBasePath);

    public string ResolvePath(string storedPath)
    {
        ValidateMediaPath(storedPath);

        if (Uri.TryCreate(storedPath, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return storedPath;
        }

        if (Path.IsPathRooted(storedPath))
        {
            return Path.GetFullPath(storedPath);
        }

        return Path.GetFullPath(storedPath, mediaRoot);
    }

    public string ToRelativePath(string absolutePath)
    {
        if (!Path.IsPathRooted(absolutePath))
        {
            throw new ArgumentException("Path must be absolute.", nameof(absolutePath));
        }

        var fullPath = Path.GetFullPath(absolutePath);
        if (!IsInsideMediaRoot(fullPath))
        {
            throw new ArgumentException("Path is outside the Gramps media root.", nameof(absolutePath));
        }

        return Path.GetRelativePath(mediaRoot, fullPath);
    }

    public bool IsInsideMediaRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.Equals(mediaRoot, StringComparison.Ordinal) ||
            fullPath.StartsWith(mediaRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    public void ValidateMediaPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Media path is required.", nameof(path));
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return;
        }
    }

    private static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Media root is required.", nameof(path));
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
