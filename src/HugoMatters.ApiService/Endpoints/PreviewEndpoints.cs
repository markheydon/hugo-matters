using HugoMatters.ApiService.Http;
using HugoMatters.Core.Api;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using HugoMatters.Core.Preview;
using HugoMatters.Core.Sessions;

namespace HugoMatters.ApiService.Endpoints;

/// <summary>
/// Editor and site preview endpoints.
/// </summary>
public static class PreviewEndpoints
{
    /// <summary>
    /// Maps preview endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapPreviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/preview").WithTags("Preview");

        group.MapPost("/editor", EditorPreview)
            .WithName("EditorPreview");

        var siteGroup = group.MapGroup("/site");

        siteGroup.MapPost("/", StartSitePreviewAsync)
            .WithName("StartSitePreview");

        siteGroup.MapGet("/", GetSitePreviewAsync)
            .WithName("GetSitePreview");

        siteGroup.MapDelete("/", StopSitePreviewAsync)
            .WithName("StopSitePreview");

        return app;
    }

    private static IResult EditorPreview(
        EditorPreviewRequest request,
        EditorPreviewService previewService)
    {
        var response = previewService.Preview(request);
        return Results.Ok(response);
    }

    private static async Task<IResult> StartSitePreviewAsync(
        SessionService sessionService,
        IMetadataStore metadataStore,
        IGitHubRepository gitHubRepository,
        ISitePreviewOrchestrator previewOrchestrator,
        CancellationToken cancellationToken)
    {
        try
        {
            var site = await metadataStore.GetConnectedSiteAsync(cancellationToken);
            if (site is null || site.Status != SiteStatus.Connected)
            {
                return ApiResults.Error("no_site", "No connected site.", StatusCodes.Status400BadRequest);
            }

            var session = await sessionService.GetActiveSessionAsync(cancellationToken);
            if (session is null)
            {
                return Results.Conflict();
            }

            if (session.HasUnsavedLocalEdits)
            {
                return ApiResults.Error(
                    "unsaved_edits",
                    "Save changes before starting a site preview.",
                    StatusCodes.Status400BadRequest);
            }

            var existing = await metadataStore.GetSitePreviewAsync(session.Id, cancellationToken);
            if (existing is not null
                && existing.Status is SitePreviewState.Starting or SitePreviewState.Running)
            {
                // Idempotent: a concurrent or retried start should observe the in-flight preview,
                // not surface a bare HTTP 409 "Conflict" in the UI.
                return Results.Ok(ToResponse(existing));
            }

            var tipSha = await gitHubRepository.GetBranchTipShaAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.BranchName,
                cancellationToken);

            var preview = await previewOrchestrator.StartPreviewAsync(
                session,
                site,
                tipSha,
                cancellationToken);

            return Results.Ok(ToResponse(preview));
        }
        catch (Octokit.NotFoundException)
        {
            return ApiResults.Error(
                "session_branch_missing",
                "The session branch no longer exists on GitHub (it may have been deleted during a failed discard). Discard this session and start a new one.",
                StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already running", StringComparison.OrdinalIgnoreCase))
        {
            var session = await sessionService.GetActiveSessionAsync(cancellationToken);
            if (session is not null)
            {
                var existing = await metadataStore.GetSitePreviewAsync(session.Id, cancellationToken);
                if (existing is not null)
                {
                    return Results.Ok(ToResponse(existing));
                }
            }

            return ApiResults.Error(
                "preview_already_running",
                "A site preview is already running for this session.",
                StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GetSitePreviewAsync(
        SessionService sessionService,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        var session = await sessionService.GetActiveSessionAsync(cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        var preview = await metadataStore.GetSitePreviewAsync(session.Id, cancellationToken);
        if (preview is null || preview.Status == SitePreviewState.Stopped)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToResponse(preview));
    }

    private static async Task<IResult> StopSitePreviewAsync(
        SessionService sessionService,
        IMetadataStore metadataStore,
        ISitePreviewOrchestrator previewOrchestrator,
        CancellationToken cancellationToken)
    {
        var session = await sessionService.GetActiveSessionAsync(cancellationToken);
        if (session is not null)
        {
            await previewOrchestrator.StopPreviewAsync(session.Id, cancellationToken);
            await metadataStore.DeleteSitePreviewAsync(session.Id, cancellationToken);
        }

        return Results.NoContent();
    }

    private static SitePreviewResponse ToResponse(SitePreviewInfo preview) => new()
    {
        Id = preview.Id,
        Status = preview.Status,
        BaseUrl = preview.BaseUrl,
        SourceRef = preview.SourceRef,
        ErrorMessage = preview.ErrorMessage,
    };
}
