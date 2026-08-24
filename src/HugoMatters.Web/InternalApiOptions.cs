namespace HugoMatters.Web;

/// <summary>
/// Shared secret used to authenticate Web → ApiService calls.
/// </summary>
public sealed class InternalApiOptions
{
    public const string SectionName = "InternalApi";

    public string SharedSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SharedSecret);
}
