namespace HugoMatter.Core.Security;

/// <summary>
/// Validates repo-relative content paths against traversal and directory allowlists.
/// </summary>
public static class ContentPathValidator
{
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>
    /// Validates a repo-relative path and returns the normalized forward-slash form.
    /// </summary>
    /// <param name="path">Path to validate.</param>
    /// <param name="allowedDirectories">Allowed root directories (e.g. <c>content/posts/</c>).</param>
    /// <returns>Normalized path using forward slashes.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is invalid.</exception>
    public static string ValidateAndNormalize(string path, IReadOnlyList<string> allowedDirectories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (path.StartsWith('/') || path.StartsWith('\\'))
        {
            throw new ArgumentException("Path must be repo-relative without a leading slash.", nameof(path));
        }

        if (Path.IsPathRooted(path))
        {
            throw new ArgumentException("Absolute paths are not allowed.", nameof(path));
        }

        var segments = path.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                throw new ArgumentException("Path must not contain '..' segments.", nameof(path));
            }

            if (segment == ".")
            {
                throw new ArgumentException("Path must not contain '.' segments.", nameof(path));
            }
        }

        var normalized = string.Join('/', segments);

        if (allowedDirectories.Count > 0 && !IsUnderAllowedDirectory(normalized, allowedDirectories))
        {
            throw new ArgumentException(
                $"Path '{normalized}' is outside allowed content directories.",
                nameof(path));
        }

        return normalized;
    }

    /// <summary>
    /// Returns whether a normalized path is under one of the allowed directories.
    /// </summary>
    public static bool IsUnderAllowedDirectory(string normalizedPath, IReadOnlyList<string> allowedDirectories)
    {
        foreach (var allowed in allowedDirectories)
        {
            var normalizedAllowed = NormalizeDirectory(allowed);
            if (normalizedPath.Equals(normalizedAllowed, StringComparison.Ordinal)
                || normalizedPath.StartsWith(normalizedAllowed + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds allowed directories from theme pack path conventions.
    /// </summary>
    public static IReadOnlyList<string> BuildAllowedDirectories(string? postsDirectory, string? pagesDirectory)
    {
        var directories = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(postsDirectory))
        {
            directories.Add(NormalizeDirectory(postsDirectory));
        }

        if (!string.IsNullOrWhiteSpace(pagesDirectory))
        {
            directories.Add(NormalizeDirectory(pagesDirectory));
        }

        return directories;
    }

    private static string NormalizeDirectory(string directory)
    {
        var trimmed = directory.Trim().TrimEnd('/', '\\');
        return trimmed.Replace('\\', '/');
    }
}
