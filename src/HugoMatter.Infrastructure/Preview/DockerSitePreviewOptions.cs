namespace HugoMatter.Infrastructure.Preview;

/// <summary>
/// Configuration for Docker/Podman Hugo site previews.
/// </summary>
public sealed class DockerSitePreviewOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "SitePreview";

    /// <summary>Hugo container image to run.</summary>
    public string HugoImage { get; set; } = "klakegg/hugo:ext";

    /// <summary>Container runtime command (<c>docker</c> or <c>podman</c>).</summary>
    public string? ContainerRuntime { get; set; }

    /// <summary>First host port to try when allocating preview ports.</summary>
    public int PortRangeStart { get; set; } = 13_130;

    /// <summary>Last host port to try when allocating preview ports.</summary>
    public int PortRangeEnd { get; set; } = 13_230;
}
