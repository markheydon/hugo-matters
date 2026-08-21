using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HugoMatter.Core.Api;
using HugoMatter.Core.Connection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HugoMatter.Infrastructure.GitHub;

/// <summary>
/// Handles GitHub App OAuth and installation binding for site connection.
/// </summary>
public sealed class GitHubConnectionHandler
{
    private readonly ConnectionService _connectionService;
    private readonly GitHubAppOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubConnectionHandler> _logger;

    /// <summary>
    /// Creates a new <see cref="GitHubConnectionHandler"/>.
    /// </summary>
    public GitHubConnectionHandler(
        ConnectionService connectionService,
        IOptions<GitHubAppOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubConnectionHandler> logger)
    {
        _connectionService = connectionService;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Starts or completes GitHub App authorization and binds the connected site.
    /// </summary>
    public async Task<AuthorizeResponse> AuthorizeAsync(
        AuthorizeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.CallbackCode))
        {
            return await CompleteOAuthAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (request.InstallationId is > 0
            && !string.IsNullOrWhiteSpace(request.Owner)
            && !string.IsNullOrWhiteSpace(request.Repo))
        {
            var site = await _connectionService.ConnectAsync(
                request.InstallationId.Value,
                request.Owner,
                request.Repo,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return new AuthorizeResponse
            {
                Status = "connected",
                Site = site,
            };
        }

        return new AuthorizeResponse
        {
            Status = "redirect",
            RedirectUrl = BuildInstallRedirectUrl(),
        };
    }

    private async Task<AuthorizeResponse> CompleteOAuthAsync(
        AuthorizeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException("GitHub App OAuth client credentials are not configured.");
        }

        var client = _httpClientFactory.CreateClient(nameof(GitHubConnectionHandler));
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = JsonContent.Create(new
            {
                client_id = _options.ClientId,
                client_secret = _options.ClientSecret,
                code = request.CallbackCode,
            }),
        };
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenPayload = await tokenResponse.Content
            .ReadFromJsonAsync<OAuthAccessTokenResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (tokenPayload is null || string.IsNullOrWhiteSpace(tokenPayload.AccessToken))
        {
            throw new InvalidOperationException("GitHub OAuth token exchange did not return an access token.");
        }

        var installationId = request.InstallationId
            ?? await ResolveInstallationIdAsync(client, tokenPayload.AccessToken, cancellationToken)
                .ConfigureAwait(false);

        if (installationId is null or <= 0)
        {
            throw new InvalidOperationException(
                "GitHub installation id was not provided and could not be resolved from the OAuth token.");
        }

        if (string.IsNullOrWhiteSpace(request.Owner) || string.IsNullOrWhiteSpace(request.Repo))
        {
            throw new InvalidOperationException(
                "Repository owner and name are required to complete GitHub App authorization.");
        }

        _logger.LogInformation(
            "Completing GitHub App authorization for {Owner}/{Repo}.",
            request.Owner,
            request.Repo);

        var site = await _connectionService.ConnectAsync(
            installationId.Value,
            request.Owner,
            request.Repo,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AuthorizeResponse
        {
            Status = "connected",
            Site = site,
        };
    }

    private async Task<long?> ResolveInstallationIdAsync(
        HttpClient client,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/user/installations");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to resolve GitHub installations with status {StatusCode}.",
                (int)response.StatusCode);
            return null;
        }

        var payload = await response.Content
            .ReadFromJsonAsync<InstallationListResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return payload?.Installations?.FirstOrDefault()?.Id;
    }

    private string BuildInstallRedirectUrl()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException("GitHub App client id is not configured.");
        }

        var redirectUri = Uri.EscapeDataString(_options.RedirectUri ?? string.Empty);
        return $"https://github.com/login/oauth/authorize?client_id={Uri.EscapeDataString(_options.ClientId)}&redirect_uri={redirectUri}";
    }

    private sealed class OAuthAccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }

    private sealed class InstallationListResponse
    {
        [JsonPropertyName("installations")]
        public List<InstallationResponse>? Installations { get; init; }
    }

    private sealed class InstallationResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }
    }
}
