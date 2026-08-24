using System.Net;
using HugoMatters.Web.Models;

namespace HugoMatters.Web;

public sealed class HugoMattersApiClient(HttpClient httpClient)
{
    public async Task<ConnectedSiteDto?> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/connection", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessOrThrow(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ConnectedSiteDto>(cancellationToken);
    }

    public async Task<RepositoryReadinessDto> GetConnectionReadinessAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/connection/readiness", cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<RepositoryReadinessDto>(cancellationToken))
            ?? new RepositoryReadinessDto { Ready = false, Message = "Could not determine repository readiness." };
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync("/api/connection", cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
    }

    public async Task<ConnectedSiteDto> ConnectAsync(long installationId, string owner, string repo, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "/api/connection",
            new ConnectRequestDto { InstallationId = installationId, Owner = owner, Repo = repo },
            cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ConnectedSiteDto>(cancellationToken))!;
    }

    public async Task<EditingSessionDto?> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/session", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessOrThrow(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<EditingSessionDto>(cancellationToken);
    }

    public async Task<EditingSessionDto> StartSessionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync("/api/session", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return (await response.Content.ReadFromJsonAsync<EditingSessionDto>(cancellationToken))!;
        }

        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<EditingSessionDto>(cancellationToken))!;
    }

    public async Task<List<ResumableSessionDto>> ListResumableSessionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/session/resumable", cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<List<ResumableSessionDto>>(cancellationToken)) ?? [];
    }

    public async Task<EditingSessionDto> ResumeSessionAsync(int pullRequestNumber, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "/api/session/resume",
            new ResumeSessionRequestDto { PullRequestNumber = pullRequestNumber },
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var existing = await response.Content.ReadFromJsonAsync<EditingSessionDto>(cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<EditingSessionDto>(cancellationToken))!;
    }

    public async Task<SaveResultDto> SaveSessionAsync(SaveRequest? request = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/session/save", request ?? new SaveRequest(), cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<SaveResultDto>(cancellationToken))!;
    }

    public async Task<PublishResultDto> PublishSessionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync("/api/session/publish", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return (await response.Content.ReadFromJsonAsync<PublishResultDto>(cancellationToken))!;
        }

        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<PublishResultDto>(cancellationToken))!;
    }

    public async Task<DiscardResultDto> DiscardSessionAsync(DiscardRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/session/discard", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return (await response.Content.ReadFromJsonAsync<DiscardResultDto>(cancellationToken))!;
        }

        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<DiscardResultDto>(cancellationToken))!;
    }

    public async Task<LeaveSessionResultDto> LeaveSessionAsync(LeaveSessionRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/session/leave", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return (await response.Content.ReadFromJsonAsync<LeaveSessionResultDto>(cancellationToken))!;
        }

        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<LeaveSessionResultDto>(cancellationToken))!;
    }

    public async Task<List<ContentItemSummaryDto>> ListContentAsync(string contentType = "all", CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/content?contentType={contentType}", cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<List<ContentItemSummaryDto>>(cancellationToken)) ?? [];
    }

    public async Task<ContentItemDto> CreateContentAsync(ContentCreateRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/content", request, cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ContentItemDto>(cancellationToken))!;
    }

    public async Task<ContentItemDto?> GetContentAsync(string path, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/content/{EncodeContentPath(path)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessOrThrow(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ContentItemDto>(cancellationToken);
    }

    public async Task<ContentItemDto> UpdateContentAsync(string path, ContentItemWrite write, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"/api/content/{EncodeContentPath(path)}", write, cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ContentItemDto>(cancellationToken))!;
    }

    public async Task DeleteContentAsync(string path, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"/api/content/{EncodeContentPath(path)}", cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
    }

    /// <summary>
    /// Encodes each path segment so slashes remain path separators (avoids %2F / double-encoding).
    /// </summary>
    private static string EncodeContentPath(string path) =>
        string.Join(
            '/',
            path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    public async Task<Dictionary<string, object?>> GetSiteConfigAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/site-config", cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken)) ?? new();
    }

    public async Task<Dictionary<string, object?>> UpdateSiteConfigAsync(Dictionary<string, object?> config, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync("/api/site-config", config, cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken)) ?? new();
    }

    public async Task<EditorPreviewResponse> PreviewEditorAsync(EditorPreviewRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/preview/editor", request, cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<EditorPreviewResponse>(cancellationToken))!;
    }

    public async Task<SitePreviewDto> StartSitePreviewAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync("/api/preview/site", null, cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<SitePreviewDto>(cancellationToken))!;
    }

    public async Task<SitePreviewDto?> GetSitePreviewAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/preview/site", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessOrThrow(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SitePreviewDto>(cancellationToken);
    }

    public async Task StopSitePreviewAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync("/api/preview/site", cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
    }

    public async Task<List<ThemePackSummaryDto>> ListThemePacksAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/theme-packs", cancellationToken);
        await EnsureSuccessOrThrow(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<List<ThemePackSummaryDto>>(cancellationToken)) ?? [];
    }

    public async Task<ThemePackDetailDto?> GetThemePackAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/theme-packs/{Uri.EscapeDataString(id)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessOrThrow(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ThemePackDetailDto>(cancellationToken);
    }

    private static async Task EnsureSuccessOrThrow(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ErrorBody? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken);
        }
        catch
        {
            // Ignore parse failures; fall back to status text.
        }

        var message = error?.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = response.StatusCode switch
            {
                HttpStatusCode.Conflict =>
                    "The request conflicted with the current preview or session state. Refresh status and try again.",
                HttpStatusCode.NotFound => "The requested resource was not found.",
                HttpStatusCode.Unauthorized => "You need to sign in again.",
                HttpStatusCode.Forbidden => "You do not have permission to do that.",
                _ => response.ReasonPhrase ?? $"Request failed ({(int)response.StatusCode}).",
            };
        }

        throw new ApiException(message, error?.Code, (int)response.StatusCode);
    }
}