using HugoMatters.Core.Models;
using Microsoft.Extensions.Logging;

namespace HugoMatters.Infrastructure.Preview;

/// <summary>
/// Reconciles <see cref="SitePreviewState.Starting"/> previews against container/runtime signals.
/// </summary>
internal static class StartingPreviewReconciler
{
    /// <summary>How long after container start before a not-ready preview is marked failed.</summary>
    public const int ReadinessTimeoutSeconds = 90;

    /// <summary>
    /// Reconciles a starting preview. Dependencies are injected so behavior can be unit tested.
    /// </summary>
    public static async Task<SitePreviewInfo> ReconcileAsync(
        SitePreviewInfo preview,
        StartingPreviewReconcileDependencies dependencies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(dependencies);

        if (string.IsNullOrWhiteSpace(preview.BaseUrl)
            || !TryGetHostPort(preview.BaseUrl, out var hostPort))
        {
            return preview;
        }

        var running = await dependencies.IsContainerRunning(cancellationToken).ConfigureAwait(false);
        if (!running)
        {
            var hugoLogs = await dependencies.GetContainerLogs(cancellationToken).ConfigureAwait(false);
            await dependencies.RemoveContainer(cancellationToken).ConfigureAwait(false);

            preview.Status = SitePreviewState.Failed;
            preview.ErrorMessage = dependencies.BuildFailureMessage(
                new InvalidOperationException("The Hugo container exited before it became ready."),
                hugoLogs);
            preview.BaseUrl = null;
            await dependencies.SavePreview(preview, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(preview.WorkspacePath))
            {
                await dependencies.CleanupWorkspace(preview.WorkspacePath!, cancellationToken)
                    .ConfigureAwait(false);
            }

            return preview;
        }

        if (await dependencies.IsPreviewReady(hostPort, cancellationToken).ConfigureAwait(false))
        {
            preview.Status = SitePreviewState.Running;
            preview.ErrorMessage = null;
            await dependencies.SavePreview(preview, cancellationToken).ConfigureAwait(false);
            return preview;
        }

        var startedAt = await dependencies.GetContainerStartedAt(cancellationToken).ConfigureAwait(false);
        if (startedAt is { } started
            && dependencies.UtcNow() - started > TimeSpan.FromSeconds(ReadinessTimeoutSeconds))
        {
            preview.Status = SitePreviewState.Failed;
            preview.ErrorMessage =
                "Hugo did not become ready within 90 seconds. You can try starting the preview again.";
            preview.BaseUrl = null;
            await dependencies.SavePreview(preview, cancellationToken).ConfigureAwait(false);
            try
            {
                await dependencies.StopContainer(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                dependencies.Logger?.LogWarning(
                    ex,
                    "Failed to stop timed-out preview container {Container}.",
                    dependencies.ContainerName);
            }

            if (!string.IsNullOrWhiteSpace(preview.WorkspacePath))
            {
                await dependencies.CleanupWorkspace(preview.WorkspacePath!, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return preview;
    }

    private static bool TryGetHostPort(string? baseUrl, out int hostPort)
    {
        hostPort = 0;
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || uri.Port <= 0)
        {
            return false;
        }

        hostPort = uri.Port;
        return true;
    }
}

/// <summary>
/// Runtime callbacks used by <see cref="StartingPreviewReconciler"/>.
/// </summary>
internal sealed class StartingPreviewReconcileDependencies
{
    public required string ContainerName { get; init; }

    public required Func<CancellationToken, Task<bool>> IsContainerRunning { get; init; }

    public required Func<CancellationToken, Task<string?>> GetContainerLogs { get; init; }

    public required Func<CancellationToken, Task> RemoveContainer { get; init; }

    public required Func<int, CancellationToken, Task<bool>> IsPreviewReady { get; init; }

    public required Func<CancellationToken, Task<DateTimeOffset?>> GetContainerStartedAt { get; init; }

    public required Func<CancellationToken, Task> StopContainer { get; init; }

    public required Func<SitePreviewInfo, CancellationToken, Task> SavePreview { get; init; }

    public required Func<string, CancellationToken, Task> CleanupWorkspace { get; init; }

    public required Func<Exception, string?, string> BuildFailureMessage { get; init; }

    public Func<DateTimeOffset> UtcNow { get; init; } = static () => DateTimeOffset.UtcNow;

    public ILogger? Logger { get; init; }
}
