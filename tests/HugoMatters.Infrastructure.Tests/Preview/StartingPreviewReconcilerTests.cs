using HugoMatters.Core.Models;
using HugoMatters.Infrastructure.Preview;

namespace HugoMatters.Infrastructure.Tests.Preview;

public sealed class StartingPreviewReconcilerTests
{
    private static readonly Guid SessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ReconcileAsync_KeepsStarting_WhenContainerRunsButPreviewNotReadyYet()
    {
        var preview = CreateStartingPreview();
        var saved = new List<SitePreviewInfo>();

        var result = await StartingPreviewReconciler.ReconcileAsync(
            preview,
            CreateDependencies(
                isContainerRunning: _ => Task.FromResult(true),
                isPreviewReady: (_, _) => Task.FromResult(false),
                getContainerStartedAt: _ => Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow),
                savePreview: (updated, _) =>
                {
                    saved.Add(Clone(updated));
                    return Task.CompletedTask;
                }),
            TestContext.Current.CancellationToken);

        Assert.Equal(SitePreviewState.Starting, result.Status);
        Assert.Empty(saved);
    }

    [Fact]
    public async Task ReconcileAsync_MarksFailed_WhenReadinessTimesOut()
    {
        var preview = CreateStartingPreview();
        var stopped = false;
        var cleaned = false;
        var startedAt = DateTimeOffset.UtcNow.AddSeconds(-(StartingPreviewReconciler.ReadinessTimeoutSeconds + 5));

        var result = await StartingPreviewReconciler.ReconcileAsync(
            preview,
            CreateDependencies(
                isContainerRunning: _ => Task.FromResult(true),
                isPreviewReady: (_, _) => Task.FromResult(false),
                getContainerStartedAt: _ => Task.FromResult<DateTimeOffset?>(startedAt),
                stopContainer: _ =>
                {
                    stopped = true;
                    return Task.CompletedTask;
                },
                cleanupWorkspace: (_, _) =>
                {
                    cleaned = true;
                    return Task.CompletedTask;
                },
                utcNow: () => DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);

        Assert.Equal(SitePreviewState.Failed, result.Status);
        Assert.Null(result.BaseUrl);
        Assert.Contains("90 seconds", result.ErrorMessage, StringComparison.Ordinal);
        Assert.True(stopped);
        Assert.True(cleaned);
    }

    private static SitePreviewInfo CreateStartingPreview() => new()
    {
        Id = "preview-test",
        SessionId = SessionId,
        Status = SitePreviewState.Starting,
        BaseUrl = "http://127.0.0.1:13130",
        WorkspacePath = "/tmp/preview-workspace",
    };

    private static StartingPreviewReconcileDependencies CreateDependencies(
        Func<CancellationToken, Task<bool>>? isContainerRunning = null,
        Func<CancellationToken, Task<string?>>? getContainerLogs = null,
        Func<CancellationToken, Task>? removeContainer = null,
        Func<int, CancellationToken, Task<bool>>? isPreviewReady = null,
        Func<CancellationToken, Task<DateTimeOffset?>>? getContainerStartedAt = null,
        Func<CancellationToken, Task>? stopContainer = null,
        Func<SitePreviewInfo, CancellationToken, Task>? savePreview = null,
        Func<string, CancellationToken, Task>? cleanupWorkspace = null,
        Func<DateTimeOffset>? utcNow = null) =>
        new()
        {
            ContainerName = "hugo-matter-preview-test",
            IsContainerRunning = isContainerRunning ?? (_ => Task.FromResult(true)),
            GetContainerLogs = getContainerLogs ?? (_ => Task.FromResult<string?>(null)),
            RemoveContainer = removeContainer ?? (_ => Task.CompletedTask),
            IsPreviewReady = isPreviewReady ?? ((_, _) => Task.FromResult(true)),
            GetContainerStartedAt = getContainerStartedAt ?? (_ => Task.FromResult<DateTimeOffset?>(null)),
            StopContainer = stopContainer ?? (_ => Task.CompletedTask),
            SavePreview = savePreview ?? ((_, _) => Task.CompletedTask),
            CleanupWorkspace = cleanupWorkspace ?? ((_, _) => Task.CompletedTask),
            BuildFailureMessage = (_, logs) => logs ?? "failed",
            UtcNow = utcNow ?? (static () => DateTimeOffset.UtcNow),
        };

    private static SitePreviewInfo Clone(SitePreviewInfo preview) => new()
    {
        Id = preview.Id,
        SessionId = preview.SessionId,
        Status = preview.Status,
        BaseUrl = preview.BaseUrl,
        WorkspacePath = preview.WorkspacePath,
        SourceRef = preview.SourceRef,
        ErrorMessage = preview.ErrorMessage,
    };
}
