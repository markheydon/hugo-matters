using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
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

            var hostPort = AllocateHostPort();
            var containerName = $"hugo-matter-preview-{session.Id:N}";
            var runtime = await ResolveContainerRuntimeAsync(cancellationToken).ConfigureAwait(false);

            await RunContainerAsync(
                runtime,
                containerName,
                workspacePath,
                hostPort,
                cancellationToken).ConfigureAwait(false);

            // Return Starting as soon as the container is launched. Waiting for Hugo here used to
            // tie readiness to the Web→API HttpClient call; Aspire's default ~10s resilience
            // timeout cancelled that wait, marked Failed, and produced Conflict on retry.
            preview.BaseUrl = $"http://127.0.0.1:{hostPort}";
            preview.Status = SitePreviewState.Starting;
            await _metadataStore.SaveSitePreviewAsync(preview, cancellationToken).ConfigureAwait(false);

            var sessionId = session.Id;
            _ = Task.Run(() => CompletePreviewStartupAsync(sessionId, hostPort));

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

    private async Task CompletePreviewStartupAsync(Guid sessionId, int hostPort)
    {
        try
        {
            using var readyCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await WaitForPreviewReadyAsync(hostPort, readyCts.Token).ConfigureAwait(false);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var metadataStore = scope.ServiceProvider.GetRequiredService<IMetadataStore>();
            var preview = await metadataStore.GetSitePreviewAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
            if (preview is null || preview.Status is not SitePreviewState.Starting)
            {
                return;
            }

            preview.Status = SitePreviewState.Running;
            preview.ErrorMessage = null;
            await metadataStore.SaveSitePreviewAsync(preview, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hugo site preview did not become ready for session {SessionId}.",
                sessionId);

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var metadataStore = scope.ServiceProvider.GetRequiredService<IMetadataStore>();
                var preview = await metadataStore.GetSitePreviewAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
                if (preview is null || preview.Status is not SitePreviewState.Starting)
                {
                    return;
                }

                preview.Status = SitePreviewState.Failed;
                preview.ErrorMessage = ex is OperationCanceledException
                    ? "Hugo preview did not become ready in time."
                    : string.IsNullOrWhiteSpace(ex.Message)
                        ? "Site preview failed to start."
                        : $"Site preview failed to start. {ex.Message}";
                await metadataStore.SaveSitePreviewAsync(preview, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to persist preview failure for session {SessionId}.", sessionId);
            }
        }
    }

    /// <inheritdoc />
    public async Task<SitePreviewInfo?> GetStatusAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var preview = await _metadataStore.GetSitePreviewAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (preview is null || preview.Status is not SitePreviewState.Running)
        {
            return preview;
        }

        var runtime = await ResolveContainerRuntimeAsync(cancellationToken).ConfigureAwait(false);
        var containerName = $"hugo-matter-preview-{sessionId:N}";
        var running = await IsContainerRunningAsync(runtime, containerName, cancellationToken).ConfigureAwait(false);
        if (running)
        {
            return preview;
        }

        preview.Status = SitePreviewState.Failed;
        preview.ErrorMessage = "Preview container is no longer running.";
        preview.BaseUrl = null;
        await _metadataStore.SaveSitePreviewAsync(preview, cancellationToken).ConfigureAwait(false);
        return preview;
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

    private async Task RunContainerAsync(
        string runtime,
        string containerName,
        string workspacePath,
        int hostPort,
        CancellationToken cancellationToken)
    {
        await StopContainerAsync(runtime, containerName, cancellationToken).ConfigureAwait(false);

        var arguments =
            $"run -d --rm --name {containerName} -p {hostPort}:1313 " +
            $"-v \"{workspacePath}:/src\" {_previewOptions.HugoImage} " +
            "server --bind 0.0.0.0 --baseURL / --port 1313 -D";

        await ExecuteRuntimeCommandAsync(runtime, arguments, cancellationToken).ConfigureAwait(false);
        // Readiness is waited separately so it can ignore inbound request cancellation.
    }

    private static async Task WaitForPreviewReadyAsync(int hostPort, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var url = $"http://127.0.0.1:{hostPort}/";

        for (var attempt = 0; attempt < 90; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Do not pass readiness CT into GetAsync — HttpClient maps its own 2s timeout to TaskCanceledException.
                using var response = await client.GetAsync(url, CancellationToken.None).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
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
