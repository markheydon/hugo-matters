using HugoMatters.ApiService.Http;
using HugoMatters.Core.Api;
using HugoMatters.Core.Connection;
using HugoMatters.Core.Models;
using HugoMatters.Infrastructure.GitHub;
using Microsoft.Extensions.Options;
using Octokit;

namespace HugoMatters.ApiService.Endpoints;

/// <summary>
/// GitHub site connection endpoints.
/// </summary>
public static class ConnectionEndpoints
{
    /// <summary>
    /// Maps connection endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/connection").WithTags("Connection");

        group.MapGet("/", GetConnectionAsync)
            .WithName("GetConnection");

        group.MapGet("/readiness", GetReadinessAsync)
            .WithName("GetConnectionReadiness");

        group.MapPost("/", ConnectAsync)
            .WithName("ConnectSite");

        group.MapDelete("/", DisconnectAsync)
            .WithName("Disconnect");

        return app;
    }

    private static async Task<IResult> GetConnectionAsync(
        ConnectionService connectionService,
        CancellationToken cancellationToken)
    {
        var site = await connectionService.GetConnectedSiteAsync(cancellationToken);
        if (site is null || site.Status == SiteStatus.Disconnected)
        {
            return Results.NotFound();
        }

        return Results.Ok(site);
    }

    private static async Task<IResult> GetReadinessAsync(
        ConnectionService connectionService,
        CancellationToken cancellationToken)
    {
        try
        {
            var readiness = await connectionService.GetSessionReadinessAsync(cancellationToken);
            return Results.Ok(readiness);
        }
        catch (Exception ex)
        {
            return Results.Ok(new RepositoryReadiness(
                Ready: false,
                Code: "readiness_check_failed",
                Message: string.IsNullOrWhiteSpace(ex.Message)
                    ? "Could not verify whether the repository is ready for editing sessions."
                    : ex.Message));
        }
    }

    private static async Task<IResult> ConnectAsync(
        ConnectRequest request,
        ConnectionService connectionService,
        IOptions<GitHubAppOptions> gitHubAppOptions,
        CancellationToken cancellationToken)
    {
        if (request.InstallationId <= 0)
        {
            return ApiResults.Error("invalid_request", "Installation id is required.", StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Owner) || string.IsNullOrWhiteSpace(request.Repo))
        {
            return ApiResults.Error("invalid_request", "Owner and repository are required.", StatusCodes.Status400BadRequest);
        }

        if (!gitHubAppOptions.Value.IsConfigured)
        {
            return ApiResults.Error(
                "github_app_not_configured",
                "GitHub App is not configured. Set AppHost parameters via aspire secret (see github-app-setup.md).",
                StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            var site = await connectionService.ConnectAsync(
                request.InstallationId,
                request.Owner.Trim(),
                request.Repo.Trim(),
                cancellationToken: cancellationToken);
            return Results.Ok(site);
        }
        catch (ArgumentException ex)
        {
            return ApiResults.Error("invalid_request", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Pull requests are disabled", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Pull requests are unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResults.Error("pull_requests_disabled", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (NotFoundException)
        {
            return ApiResults.Error(
                "repository_not_found",
                $"Repository '{request.Owner.Trim()}/{request.Repo.Trim()}' was not found, or this GitHub App installation cannot access it.",
                StatusCodes.Status404NotFound);
        }
        catch (AuthorizationException)
        {
            return ApiResults.Error(
                "github_app_unauthorized",
                "GitHub rejected the App credentials. Check App ID and private key (aspire secret set), then restart ApiService.",
                StatusCodes.Status401Unauthorized);
        }
        catch (ApiException ex) when ((int)ex.StatusCode is >= 400 and < 500)
        {
            return ApiResults.Error(
                "github_request_failed",
                string.IsNullOrWhiteSpace(ex.Message)
                    ? "GitHub rejected the connect request."
                    : ex.Message,
                (int)ex.StatusCode);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("private key", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("do not match", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Integration not found", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("same GitHub App", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResults.Error(
                "github_app_misconfigured",
                ex.Message,
                StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> DisconnectAsync(
        ConnectionService connectionService,
        CancellationToken cancellationToken)
    {
        try
        {
            await connectionService.DisconnectAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("active_session", ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}
