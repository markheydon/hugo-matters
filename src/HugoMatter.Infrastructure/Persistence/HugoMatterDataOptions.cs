namespace HugoMatter.Infrastructure.Persistence;

/// <summary>
/// Local application data path configuration.
/// </summary>
public sealed class HugoMatterDataOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "HugoMatter";

    /// <summary>
    /// Directory for SQLite database, preview workspaces, and other local state.
    /// Defaults to <c>~/.hugo-matter</c> when unset.
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>
    /// Resolves the configured data directory or the default user-local path.
    /// </summary>
    public string ResolveDataDirectory()
    {
        if (!string.IsNullOrWhiteSpace(DataDirectory))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(DataDirectory));
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".hugo-matter");
    }
}
