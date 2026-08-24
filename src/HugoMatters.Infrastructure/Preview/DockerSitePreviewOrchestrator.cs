using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using HugoMatters.Core.Content;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using HugoMatters.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HugoMatters.Infrastructure.Preview;

/// <summary>
/// Runs real Hugo site previews in isolated Docker/Podman containers.
/// </summary>
public sealed class DockerSitePreviewOrchestrator : ISitePreviewOrchestrator
{
    private readonly IMetadataStore _metadataStore;
    private readonly IGitHubRepository _gitHubRepository;
    private readonly HugoMattersDataOptions _dataOptions;
    private readonly DockerSitePreviewOptions _previewOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DockerSitePreviewOrchestrator> _logger;
    private readonly SemaphoreSlim _runtimeLock = new(1, 1);
    private string? _containerRuntime;

    /// <summary>
    /// Creates a new <see cref="DockerSitePreviewOrchestrator"/>.
    /// </summary>
    public DockerSitePreviewOrchestrator(
        IMetadataStore metadataStore,
        IGitHubRepository gitHubRepository,
        IOptions<HugoMattersDataOptions> dataOptions,
        IOptions<DockerSitePreviewOptions> previewOptions,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<DockerSitePreviewOrchestrator> logger)
    {
        _metadataStore = metadataStore;
        _gitHubRepository = gitHubRepository;
        _dataOptions = dataOptions.Value;
        _previewOptions = previewOptions.Value;
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SitePreviewInfo> StartPreviewAsync(
        EditingSession session,
        ConnectedSite site,
        string branchTipSha,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchTipSha);

        await StopPreviewAsync(session.Id, cancellationToken).ConfigureAwait(false);

        var previewId = $"preview-{session.Id:N}";
        var workspacePath = Path.Combine(
            _dataOptions.ResolveDataDirectory(),
            "previews",
            session.Id.ToString("N"));

        var preview = new SitePreviewInfo
        {
            Id = previewId,
            SessionId = session.Id,
            Status = SitePreviewState.Starting,
            WorkspacePath = workspacePath,
            SourceRef = branchTipSha,
        };

        await _metadataStore.SaveSitePreviewAsync(preview, cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(workspacePath);
            await DownloadSavedBranchSnapshotAsync(
                site,
                branchTipSha,
                workspacePath,
                cancellationToken).ConfigureAwait(false);

            // Repair frontmatter corrupted by prior JsonElement→string round-trips (e.g. tags).
            RepairFrontMatterInWorkspace(workspacePath);

            // Return Starting after the workspace is ready. Hugo build + server start finish in
            // the background so the Web→API call stays under Aspire's resilience timeouts.
            preview.Status = SitePreviewState.Starting;
            preview.BaseUrl = null;
            preview.ErrorMessage = null;
            await _metadataStore.SaveSitePreviewAsync(preview, cancellationToken).ConfigureAwait(false);

            var sessionId = session.Id;
            _ = Task.Run(() => CompletePreviewStartupAsync(sessionId, workspacePath));

            return preview;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to start Hugo site preview for session {SessionId}.",
                session.Id);

            preview.Status = SitePreviewState.Failed;
            preview.ErrorMessage = ex is OperationCanceledException
                ? "Site preview start was cancelled before the Hugo container launched. Try again."
                : string.IsNullOrWhiteSpace(ex.Message)
                    ? "Site preview failed to start."
                    : $"Site preview failed to start. {ex.Message}";
            await _metadataStore.SaveSitePreviewAsync(preview, CancellationToken.None).ConfigureAwait(false);
            await CleanupWorkspaceAsync(workspacePath).ConfigureAwait(false);
            return preview;
        }
    }

    private async Task CompletePreviewStartupAsync(Guid sessionId, string workspacePath)
    {
        string? runtime = null;
        string? containerName = null;
        try
        {
            runtime = await ResolveContainerRuntimeAsync(CancellationToken.None).ConfigureAwait(false);
            containerName = $"hugo-matter-preview-{sessionId:N}";

            // Fail fast with Hugo's own diagnostics before starting the long-lived server.
            await RunHugoBuildAsync(runtime, containerName, workspacePath, CancellationToken.None)
                .ConfigureAwait(false);

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var metadataStore = scope.ServiceProvider.GetRequiredService<IMetadataStore>();
                var preview = await metadataStore.GetSitePreviewAsync(sessionId, CancellationToken.None)
                    .ConfigureAwait(false);
                if (preview is null || preview.Status is not SitePreviewState.Starting)
                {
                    return;
                }
            }

            var hostPort = AllocateHostPort();
            await RunContainerAsync(
                runtime,
                containerName,
                workspacePath,
                hostPort,
                CancellationToken.None).ConfigureAwait(false);

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var metadataStore = scope.ServiceProvider.GetRequiredService<IMetadataStore>();
                var preview = await metadataStore.GetSitePreviewAsync(sessionId, CancellationToken.None)
                    .ConfigureAwait(false);
                if (preview is null || preview.Status is not SitePreviewState.Starting)
                {
                    await TryRemoveContainerAsync(runtime, containerName, CancellationToken.None)
                        .ConfigureAwait(false);
                    return;
                }

                // Readiness probes use loopback (API and Hugo share the Linux/WSL host).
                // The URL shown to the browser must be reachable from outside that host —
                // on WSL2, Windows localhost often cannot reach rootless Podman publishes.
                preview.BaseUrl = $"http://{ResolveBrowserPreviewHost()}:{hostPort}";
                await metadataStore.SaveSitePreviewAsync(preview, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            using var readyCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await WaitForPreviewReadyAsync(hostPort, readyCts.Token).ConfigureAwait(false);

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var metadataStore = scope.ServiceProvider.GetRequiredService<IMetadataStore>();
                var preview = await metadataStore.GetSitePreviewAsync(sessionId, CancellationToken.None)
                    .ConfigureAwait(false);
                if (preview is null || preview.Status is not SitePreviewState.Starting)
                {
                    return;
                }

                preview.Status = SitePreviewState.Running;
                preview.ErrorMessage = null;
                await metadataStore.SaveSitePreviewAsync(preview, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hugo site preview for session {SessionId} did not become ready.",
                sessionId);

            string? hugoLogs = null;
            if (runtime is not null && containerName is not null)
            {
                hugoLogs = await TryGetContainerLogsAsync(runtime, containerName, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            // One-shot build failures put Hugo output in the exception message already.
            hugoLogs ??= ex.Message;

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var metadataStore = scope.ServiceProvider.GetRequiredService<IMetadataStore>();
                var preview = await metadataStore.GetSitePreviewAsync(sessionId, CancellationToken.None)
                    .ConfigureAwait(false);
                if (preview is null || preview.Status is not SitePreviewState.Starting)
                {
                    return;
                }

                preview.Status = SitePreviewState.Failed;
                preview.ErrorMessage = BuildHugoFailureMessage(ex, hugoLogs);
                preview.BaseUrl = null;
                await metadataStore.SaveSitePreviewAsync(preview, CancellationToken.None)
                    .ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(preview.WorkspacePath))
                {
                    await CleanupWorkspaceAsync(preview.WorkspacePath).ConfigureAwait(false);
                }
            }
            catch (Exception saveEx)
            {
                _logger.LogError(
                    saveEx,
                    "Failed to persist Failed status for site preview session {SessionId}.",
                    sessionId);
            }
            finally
            {
                if (runtime is not null && containerName is not null)
                {
                    await TryRemoveContainerAsync(runtime, containerName, CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    private static string BuildHugoFailureMessage(Exception ex, string? hugoLogs)
    {
        var hugoError = ExtractHugoError(hugoLogs);
        if (!string.IsNullOrWhiteSpace(hugoError))
        {
            return $"Hugo failed to build the site preview: {hugoError}";
        }

        if (ex is OperationCanceledException)
        {
            return "Hugo preview did not become ready in time.";
        }

        return string.IsNullOrWhiteSpace(ex.Message)
            ? "Site preview failed to start."
            : $"Site preview failed to start. {ex.Message}";
    }

    private static string? ExtractHugoError(string? logs)
    {
        if (string.IsNullOrWhiteSpace(logs))
        {
            return null;
        }

        // Prefer the last ERROR line from Hugo output.
        string? lastError = null;
        using var reader = new StringReader(logs);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("error building site", StringComparison.OrdinalIgnoreCase))
            {
                lastError = trimmed;
                if (lastError.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    lastError = lastError["ERROR".Length..].TrimStart(' ', ':');
                }
            }
        }

        if (string.IsNullOrWhiteSpace(lastError))
        {
            return null;
        }

        return lastError.Length > 400 ? lastError[..400] + "…" : lastError;
    }

    /// <inheritdoc />
    public async Task<SitePreviewInfo?> GetStatusAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var preview = await _metadataStore.GetSitePreviewAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (preview is null)
        {
            return null;
        }

        // Terminal states are returned as stored; live states are re-checked against the container/Hugo.
        if (preview.Status is SitePreviewState.Failed or SitePreviewState.Stopped)
        {
            return preview;
        }

        try
        {
            if (preview.Status is SitePreviewState.Starting)
            {
                return await ReconcileStartingPreviewAsync(preview, sessionId, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Running — confirm the container is still up.
            var runtime = await ResolveContainerRuntimeAsync(cancellationToken).ConfigureAwait(false);
            var containerName = $"hugo-matter-preview-{sessionId:N}";
            var running = await IsContainerRunningAsync(runtime, containerName, cancellationToken)
                .ConfigureAwait(false);
            if (running)
            {
                // Also verify Hugo still answers; a dead process in a zombie container is not Running.
                if (TryGetHostPort(preview.BaseUrl, out var hostPort)
                    && !await IsPreviewReadyAsync(hostPort, cancellationToken).ConfigureAwait(false))
                {
                    preview.Status = SitePreviewState.Failed;
                    preview.ErrorMessage = "Preview container is running but Hugo is not responding.";
                    preview.BaseUrl = null;
                    await _metadataStore.SaveSitePreviewAsync(preview, cancellationToken).ConfigureAwait(false);
                }

                return preview;
            }

            preview.Status = SitePreviewState.Failed;
            preview.ErrorMessage = "Preview container is no longer running.";
            preview.BaseUrl = null;
            await _metadataStore.SaveSitePreviewAsync(preview, cancellationToken).ConfigureAwait(false);
            return preview;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to reconcile site preview status for session {SessionId}.",
                sessionId);

            preview.Status = SitePreviewState.Failed;
            preview.ErrorMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Could not verify preview status."
                : $"Could not verify preview status. {ex.Message}";
            preview.BaseUrl = null;
            await _metadataStore.SaveSitePreviewAsync(preview, CancellationToken.None).ConfigureAwait(false);
            return preview;
        }
    }

    private async Task<SitePreviewInfo> ReconcileStartingPreviewAsync(
        SitePreviewInfo preview,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        // BaseUrl is assigned only after the preflight Hugo build succeeds and the server
        // container is launched. Until then, leave status as Starting.
        if (string.IsNullOrWhiteSpace(preview.BaseUrl) || !TryGetHostPort(preview.BaseUrl, out var hostPort))
        {
            return preview;
        }

        var runtime = await ResolveContainerRuntimeAsync(cancellationToken).ConfigureAwait(false);
        var containerName = $"hugo-matter-preview-{sessionId:N}";
        var running = await IsContainerRunningAsync(runtime, containerName, cancellationToken).ConfigureAwait(false);
        if (!running)
        {
            var hugoLogs = await TryGetContainerLogsAsync(runtime, containerName, cancellationToken)
                .ConfigureAwait(false);
            await TryRemoveContainerAsync(runtime, containerName, cancellationToken).ConfigureAwait(false);

            preview.Status = SitePreviewState.Failed;
            preview.ErrorMessage = BuildHugoFailureMessage(
                new InvalidOperationException("The Hugo container exited before it became ready."),
                hugoLogs);
            preview.BaseUrl = null;
            await _metadataStore.SaveSitePreviewAsync(preview, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(preview.WorkspacePath))
            {
                await CleanupWorkspaceAsync(preview.WorkspacePath).ConfigureAwait(false);
            }

            return preview;
        }

        if (await IsPreviewReadyAsync(hostPort, cancellationToken).ConfigureAwait(false))
        {
            preview.Status = SitePreviewState.Running;
            preview.ErrorMessage = null;
            await _metadataStore.SaveSitePreviewAsync(preview, cancellationToken).ConfigureAwait(false);
            return preview;
        }

        // Container is up but Hugo still isn't answering. If it's been long enough, treat as Failed
        // so Refresh/status checks don't leave the UI stuck on Starting forever.
        var startedAt = await GetContainerStartedAtAsync(runtime, containerName, cancellationToken)
            .ConfigureAwait(false);
        if (startedAt is { } started
            && DateTimeOffset.UtcNow - started > TimeSpan.FromSeconds(90))
        {
            preview.Status = SitePreviewState.Failed;
            preview.ErrorMessage =
                "Hugo did not become ready within 90 seconds. You can try starting the preview again.";
            preview.BaseUrl = null;
            await _metadataStore.SaveSitePreviewAsync(preview, cancellationToken).ConfigureAwait(false);
            try
            {
                await StopContainerAsync(runtime, containerName, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to stop timed-out preview container {Container}.", containerName);
            }

            if (!string.IsNullOrWhiteSpace(preview.WorkspacePath))
            {
                await CleanupWorkspaceAsync(preview.WorkspacePath).ConfigureAwait(false);
            }
        }

        return preview;
    }

    private async Task<DateTimeOffset?> GetContainerStartedAtAsync(
        string runtime,
        string containerName,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteRuntimeCommandAsync(
            runtime,
            $"inspect -f \"{{{{.State.StartedAt}}}}\" {containerName}",
            cancellationToken,
            ignoreErrors: true).ConfigureAwait(false);

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            result.StandardOutput.Trim(),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var startedAt)
            ? startedAt
            : null;
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

    private static async Task<bool> IsPreviewReadyAsync(int hostPort, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            using var response = await client
                .GetAsync($"http://127.0.0.1:{hostPort}/", cancellationToken)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task StopPreviewAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var preview = await _metadataStore.GetSitePreviewAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (preview is null)
        {
            return;
        }

        // Only touch the container runtime when a container may still be running.
        // Failed/stopped previews (or machines without Docker) must still clean up metadata.
        if (preview.Status is SitePreviewState.Running or SitePreviewState.Starting)
        {
            try
            {
                var runtime = await ResolveContainerRuntimeAsync(cancellationToken).ConfigureAwait(false);
                var containerName = $"hugo-matter-preview-{sessionId:N}";
                await StopContainerAsync(runtime, containerName, cancellationToken).ConfigureAwait(false);
                await TryRemoveContainerAsync(runtime, containerName, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to stop preview container for session {SessionId}; continuing cleanup.",
                    sessionId);
            }
        }

        if (!string.IsNullOrWhiteSpace(preview.WorkspacePath))
        {
            await CleanupWorkspaceAsync(preview.WorkspacePath).ConfigureAwait(false);
        }

        preview.Status = SitePreviewState.Stopped;
        preview.BaseUrl = null;
        preview.ErrorMessage = null;
        await _metadataStore.SaveSitePreviewAsync(preview, cancellationToken).ConfigureAwait(false);
    }

    private async Task DownloadSavedBranchSnapshotAsync(
        ConnectedSite site,
        string branchTipSha,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var token = await _gitHubRepository.GetInstallationTokenAsync(site.InstallationId, cancellationToken)
            .ConfigureAwait(false);
        var client = _httpClientFactory.CreateClient(nameof(DockerSitePreviewOrchestrator));

        var archiveUrl =
            $"https://api.github.com/repos/{site.OwnerLogin}/{site.RepoName}/tarball/{branchTipSha}";
        using var request = new HttpRequestMessage(HttpMethod.Get, archiveUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("User-Agent", "HugoMatters");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body.Trim();
            if (detail is { Length: > 300 })
            {
                detail = detail[..300];
            }

            throw new InvalidOperationException(
                $"GitHub authorization failed while preparing site preview ({(int)response.StatusCode}). {detail}");
        }

        response.EnsureSuccessStatusCode();

        await using var archiveStream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream);

        while (tarReader.GetNextEntry() is { } entry)
        {
            if (entry.EntryType is not TarEntryType.RegularFile || entry.DataStream is null)
            {
                continue;
            }

            var relativePath = entry.Name;
            var slashIndex = relativePath.IndexOf('/');
            if (slashIndex >= 0)
            {
                relativePath = relativePath[(slashIndex + 1)..];
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var destinationPath = Path.Combine(workspacePath, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            await using var destination = File.Create(destinationPath);
            await entry.DataStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunHugoBuildAsync(
        string runtime,
        string containerName,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var buildContainerName = $"{containerName}-build";
        await TryRemoveContainerAsync(runtime, buildContainerName, cancellationToken).ConfigureAwait(false);

        var arguments =
            $"run --rm --name {buildContainerName} " +
            $"-v \"{workspacePath}:/src\" {_previewOptions.HugoImage}";

        try
        {
            await ExecuteRuntimeCommandAsync(runtime, arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            var hugoError = ExtractHugoError(ex.Message);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(hugoError)
                    ? "Hugo build failed before the preview server could start."
                    : $"Hugo build failed before the preview server could start: {hugoError}",
                ex);
        }
    }

    private async Task RunContainerAsync(
        string runtime,
        string containerName,
        string workspacePath,
        int hostPort,
        CancellationToken cancellationToken)
    {
        await StopContainerAsync(runtime, containerName, cancellationToken).ConfigureAwait(false);
        await TryRemoveContainerAsync(runtime, containerName, cancellationToken).ConfigureAwait(false);

        var arguments =
            $"run -d --name {containerName} -p {hostPort}:1313 " +
            $"-v \"{workspacePath}:/src\" {_previewOptions.HugoImage} " +
            "server --bind 0.0.0.0 --baseURL / --port 1313 -D";

        await ExecuteRuntimeCommandAsync(runtime, arguments, cancellationToken).ConfigureAwait(false);
    }

    private static void RepairFrontMatterInWorkspace(string workspacePath)
    {
        if (!Directory.Exists(workspacePath))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(workspacePath, "*.md", SearchOption.AllDirectories))
        {
            try
            {
                var original = File.ReadAllText(path);
                if (!original.StartsWith("---", StringComparison.Ordinal))
                {
                    continue;
                }

                var document = HugoContentDocument.Parse(original);
                var repaired = document.Serialize();
                if (!string.Equals(original, repaired, StringComparison.Ordinal))
                {
                    File.WriteAllText(path, repaired);
                }
            }
            catch
            {
                // Leave individual files alone if they cannot be parsed.
            }
        }
    }

    private async Task<string?> TryGetContainerLogsAsync(
        string runtime,
        string containerName,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteRuntimeCommandAsync(
                runtime,
                $"logs {containerName}",
                cancellationToken,
                ignoreErrors: true).ConfigureAwait(false);

            var combined = string.Join(
                Environment.NewLine,
                new[] { result.StandardOutput, result.StandardError }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

            return string.IsNullOrWhiteSpace(combined) ? null : combined;
        }
        catch
        {
            return null;
        }
    }

    private async Task TryRemoveContainerAsync(
        string runtime,
        string containerName,
        CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteRuntimeCommandAsync(
                runtime,
                $"rm -f {containerName}",
                cancellationToken,
                ignoreErrors: true).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static async Task WaitForPreviewReadyAsync(int hostPort, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsPreviewReadyAsync(hostPort, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Hugo preview did not become ready in time.");
    }

    private async Task StopContainerAsync(
        string runtime,
        string containerName,
        CancellationToken cancellationToken)
    {
        if (!await IsContainerRunningAsync(runtime, containerName, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await ExecuteRuntimeCommandAsync(runtime, $"stop {containerName}", cancellationToken, ignoreErrors: true)
            .ConfigureAwait(false);
    }

    private async Task<bool> IsContainerRunningAsync(
        string runtime,
        string containerName,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteRuntimeCommandAsync(
            runtime,
            $"inspect -f \"{{{{.State.Running}}}}\" {containerName}",
            cancellationToken,
            ignoreErrors: true).ConfigureAwait(false);

        return result.ExitCode == 0
            && result.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> ResolveContainerRuntimeAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_containerRuntime))
        {
            return _containerRuntime;
        }

        await _runtimeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(_containerRuntime))
            {
                return _containerRuntime;
            }

            if (!string.IsNullOrWhiteSpace(_previewOptions.ContainerRuntime))
            {
                _containerRuntime = _previewOptions.ContainerRuntime;
                return _containerRuntime;
            }

            // Prefer podman when both exist; docker probe must not throw if the binary is missing.
            foreach (var candidate in new[] { "podman", "docker" })
            {
                var result = await ExecuteRuntimeCommandAsync(
                    candidate,
                    "version",
                    cancellationToken,
                    ignoreErrors: true).ConfigureAwait(false);
                if (result.ExitCode == 0)
                {
                    _containerRuntime = candidate;
                    return _containerRuntime;
                }
            }

            throw new InvalidOperationException(
                "Neither Podman nor Docker is available for site preview. Install one of them, or set SitePreview:ContainerRuntime.");
        }
        finally
        {
            _runtimeLock.Release();
        }
    }

    private int AllocateHostPort()
    {
        for (var port = _previewOptions.PortRangeStart; port <= _previewOptions.PortRangeEnd; port++)
        {
            if (IsPortAvailable(port))
            {
                return port;
            }
        }

        throw new InvalidOperationException("No available host port was found for site preview.");
    }

    /// <summary>
    /// Host name/IP for links opened in the owner's browser. Prefer a WSL LAN address when
    /// Windows localhost cannot reach container-published ports.
    /// </summary>
    private static string ResolveBrowserPreviewHost()
    {
        if (IsRunningInWsl())
        {
            var wslHost = TryGetPreferredLanIpv4();
            if (!string.IsNullOrWhiteSpace(wslHost))
            {
                return wslHost;
            }
        }

        return "127.0.0.1";
    }

    private static bool IsRunningInWsl()
    {
        try
        {
            return OperatingSystem.IsLinux()
                && File.Exists("/proc/version")
                && File.ReadAllText("/proc/version")
                    .Contains("microsoft", StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string? TryGetPreferredLanIpv4()
    {
        string? fallback = null;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up
                || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var address in nic.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork
                    || IPAddress.IsLoopback(address.Address))
                {
                    continue;
                }

                var ip = address.Address.ToString();
                if (nic.Name.Contains("eth", StringComparison.OrdinalIgnoreCase))
                {
                    return ip;
                }

                fallback ??= ip;
            }
        }

        return fallback;
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static async Task CleanupWorkspaceAsync(string workspacePath)
    {
        if (!Directory.Exists(workspacePath))
        {
            return;
        }

        try
        {
            Directory.Delete(workspacePath, recursive: true);
        }
        catch (IOException)
        {
            await Task.Delay(250).ConfigureAwait(false);
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    private static async Task<ProcessResult> ExecuteRuntimeCommandAsync(
        string runtime,
        string arguments,
        CancellationToken cancellationToken,
        bool ignoreErrors = false)
    {
        var startInfo = new ProcessStartInfo(runtime, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            // Missing binary (e.g. docker not installed) throws on Start — treat as unavailable
            // so runtime detection can fall through to podman.
            if (ignoreErrors)
            {
                return new ProcessResult(-1, string.Empty, ex.Message);
            }

            throw new InvalidOperationException(
                $"Failed to start '{runtime}': {ex.Message}",
                ex);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var result = new ProcessResult(
            process.ExitCode,
            await stdoutTask.ConfigureAwait(false),
            await stderrTask.ConfigureAwait(false));

        if (!ignoreErrors && result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput.Trim()
                : result.StandardError.Trim();
            if (detail.Length > 500)
            {
                detail = detail[..500];
            }

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(detail)
                    ? $"{runtime} command failed with exit code {result.ExitCode}."
                    : $"{runtime} command failed with exit code {result.ExitCode}: {detail}");
        }

        return result;
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
