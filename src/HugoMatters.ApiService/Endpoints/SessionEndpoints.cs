using HugoMatters.Core.Api;
using HugoMatters.Core.Sessions;
using HugoMatters.ApiService.Http;

namespace HugoMatters.ApiService.Endpoints;

/// <summary>
/// Editing session lifecycle endpoints.
/// </summary>
public static class SessionEndpoints
{
    /// <summary>
    /// Maps session endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/session").WithTags("Session");

        group.MapGet("/", GetSessionAsync)
            .WithName("GetSession");

        group.MapPost("/", StartSessionAsync)
            .WithName("StartSession");

        group.MapPost("/save", SaveAsync)
            .WithName("SaveSession");

        group.MapPost("/publish", PublishAsync)
            .WithName("PublishSession");

        group.MapPost("/discard", DiscardAsync)
            .WithName("DiscardSession");

        return app;
    }

    private static async Task<IResult> GetSessionAsync(
        SessionService sessionService,
        CancellationToken cancellationToken)
    {
        var session = await sessionService.GetActiveSessionAsync(cancellationToken);
        return session is null ? Results.NotFound() : Results.Ok(session);
    }

    private static async Task<IResult> StartSessionAsync(
        SessionService sessionService,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await sessionService.StartSessionAsync(cancellationToken);
            return Results.Created("/api/session", session);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            var existing = await sessionService.GetActiveSessionAsync(cancellationToken);
            return Results.Conflict(existing);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApiResults.Error("unauthorized", ex.Message, StatusCodes.Status401Unauthorized);
        }
    }

    private static async Task<IResult> SaveAsync(
        SaveRequest? request,
        SaveService saveService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await saveService.SaveAsync(request, cancellationToken);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status409Conflict);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApiResults.Error("unauthorized", ex.Message, StatusCodes.Status401Unauthorized);
        }
    }

    private static async Task<IResult> PublishAsync(
        PublishService publishService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await publishService.PublishAsync(cancellationToken);
            if (result.Outcome == HugoMatters.Core.Models.PublishOutcome.Succeeded)
            {
                return Results.Ok(result);
            }

            return Results.Json(result, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApiResults.Error("unauthorized", ex.Message, StatusCodes.Status401Unauthorized);
        }
    }

    private static async Task<IResult> DiscardAsync(
        DiscardRequest request,
        DiscardService discardService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await discardService.DiscardAsync(request, cancellationToken);
            if (result.Outcome == HugoMatters.Core.Models.DiscardOutcome.Succeeded)
            {
                return Results.Ok(result);
            }

            return Results.Json(result, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}
