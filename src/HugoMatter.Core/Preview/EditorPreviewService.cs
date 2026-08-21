using HugoMatter.Core.Api;
using Markdig;

namespace HugoMatter.Core.Preview;

/// <summary>
/// Renders approximate in-editor HTML preview from Markdown using Markdig.
/// </summary>
public sealed class EditorPreviewService
{
    private readonly MarkdownPipeline _pipeline;

    /// <summary>
    /// Creates a new <see cref="EditorPreviewService"/>.
    /// </summary>
    public EditorPreviewService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    /// <summary>
    /// Renders Markdown body to approximate HTML. Shortcodes are not evaluated.
    /// </summary>
    public EditorPreviewResponse Preview(EditorPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var html = Markdown.ToHtml(request.Body ?? string.Empty, _pipeline);
        return new EditorPreviewResponse { Html = html };
    }
}
