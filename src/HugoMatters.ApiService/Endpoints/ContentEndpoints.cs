using HugoMatters.ApiService.Http;
using HugoMatters.Core.Api;
using HugoMatters.Core.Content;
using HugoMatters.Core.Models;

namespace HugoMatters.ApiService.Endpoints;

/// <summary>
/// Content CRUD endpoints for the active session buffer.
/// </summary>
public static class ContentEndpoints
{
    /// <summary>
    /// Maps content endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/content").WithTags("Content");

        group.MapGet("/", ListContentAsync)
            .WithName("ListContent");

        group.MapPost("/", CreateContentAsync)
            .WithName("CreateContent");

        group.MapGet("/{*path}", GetContentAsync)
            .WithName("GetContent");

        group.MapPut("/{*path}", UpsertContentAsync)
            .WithName("UpsertContent");

        group.MapDelete("/{*path}", DeleteContentAsync)
            .WithName("DeleteContent");

        return app;
    }

    private static async Task<IResult> ListContentAsync(
        string? contentType,
        ContentBufferService contentService,
        CancellationToken cancellationToken)
    {
        try
        {
            var filter = ParseContentTypeFilter(contentType);
            var items = await contentService.ListAsync(filter, cancellationToken);
            return Results.Ok(items);
        }
        catch (ArgumentException ex)
        {
            return ApiResults.Error("invalid_request", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("session", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> CreateContentAsync(
        ContentCreateRequest request,
        ContentBufferService contentService,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await contentService.CreateAsync(
                request.ContentType,
                request.Slug,
                request.Title,
                cancellationToken);

            return Results.Created($"/api/content/{Uri.EscapeDataString(item.Path)}", item);
        }
        catch (ArgumentException ex)
        {
            return ApiResults.Error("invalid_request", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GetContentAsync(
        string path,
        ContentBufferService contentService,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalized = path.TrimStart('/');
            // Blazor / HTTP may leave %2F encoded when the UI double-escaped the path.
            if (normalized.Contains("%2F", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("%2f", StringComparison.Ordinal))
            {
                normalized = Uri.UnescapeDataString(normalized);
            }

            var item = await contentService.GetAsync(normalized, cancellationToken);
            return item is null
                ? ApiResults.Error("not_found", "Content item was not found.", StatusCodes.Status404NotFound)
                : Results.Ok(item);
        }
        catch (ArgumentException ex)
        {
            return ApiResults.Error("invalid_path", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> UpsertContentAsync(
        string path,
        ContentItemWrite request,
        ContentBufferService contentService,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalized = path.TrimStart('/');
            var item = await contentService.UpsertAsync(
                normalized,
                request.FrontMatter,
                request.Body,
                cancellationToken);

            return Results.Ok(item);
        }
        catch (ArgumentException ex)
        {
            return ApiResults.Error("invalid_request", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> DeleteContentAsync(
        string path,
        ContentBufferService contentService,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalized = path.TrimStart('/');
            var deleted = await contentService.DeleteAsync(normalized, cancellationToken);
            return deleted
                ? Results.NoContent()
                : ApiResults.Error("not_found", "Content item was not found.", StatusCodes.Status404NotFound);
        }
        catch (ArgumentException ex)
        {
            return ApiResults.Error("invalid_path", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static ContentTypeKind? ParseContentTypeFilter(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (contentType.Equals("post", StringComparison.OrdinalIgnoreCase))
        {
            return ContentTypeKind.Post;
        }

        if (contentType.Equals("page", StringComparison.OrdinalIgnoreCase))
        {
            return ContentTypeKind.Page;
        }

        throw new ArgumentException($"Unknown content type filter '{contentType}'.");
    }
}
