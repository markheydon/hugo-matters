using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace HugoMatters.Web.Authentication;

/// <summary>
/// GitHub App user sign-in handshake (authorize URL, token exchange, session claims).
/// </summary>
public sealed class GitHubAuthGateway(
    IHttpClientFactory httpClientFactory,
    IOptions<GitHubAuthOptions> authOptions)
{
    internal const string GitHubAuthClientName = "GitHubAuthClient";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly GitHubAuthOptions _authOptions = authOptions.Value;

    internal string BuildAuthoriseUrl(string state, string redirectUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);
        EnsureSignInConfiguration();

        return QueryHelpers.AddQueryString(
            _authOptions.HostedGitHubAuthoriseEndpoint,
            new Dictionary<string, string?>
            {
                ["client_id"] = _authOptions.HostedGitHubAppClientId,
                ["redirect_uri"] = redirectUri,
                ["scope"] = _authOptions.HostedSignInScopes,
                ["state"] = state,
            });
    }

    internal async Task<GitHubAuthSession> ExchangeCodeForSessionAsync(string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        EnsureSignInConfiguration();

        var client = _httpClientFactory.CreateClient(GitHubAuthClientName);
        var tokenResponse = await ExchangeCodeForAccessTokenAsync(client, code, cancellationToken).ConfigureAwait(false);
        var user = await GetAuthenticatedUserAsync(client, tokenResponse.AccessToken, cancellationToken).ConfigureAwait(false);
        var installationId = await ResolveInstallationIdAsync(client, tokenResponse.AccessToken, user.Login, cancellationToken).ConfigureAwait(false);

        return new GitHubAuthSession(
            user.Login,
            tokenResponse.AccessToken,
            installationId,
            ComputeExpiresAtUtc(tokenResponse.ExpiresInSeconds),
            tokenResponse.RefreshToken ?? string.Empty,
            ComputeExpiresAtUtc(tokenResponse.RefreshTokenExpiresInSeconds));
    }

    /// <summary>
    /// Lists repositories accessible to the signed-in user through a GitHub App installation.
    /// </summary>
    internal async Task<IReadOnlyList<GitHubInstallationRepository>> ListInstallationRepositoriesAsync(
        string accessToken,
        long installationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        if (installationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(installationId));
        }

        var client = _httpClientFactory.CreateClient(GitHubAuthClientName);
        var repositories = new List<GitHubInstallationRepository>();
        var page = 1;

        while (true)
        {
            using var request = CreateGitHubApiRequest(
                HttpMethod.Get,
                $"/user/installations/{installationId}/repositories?per_page=100&page={page}",
                accessToken);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            EnsureSuccessStatusCode(response);

            var payload = await response.Content.ReadFromJsonAsync<InstallationRepositoriesResponseDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("GitHub returned an empty repositories response.");

            foreach (var repository in payload.Repositories)
            {
                if (string.IsNullOrWhiteSpace(repository.FullName)
                    || string.IsNullOrWhiteSpace(repository.Owner?.Login)
                    || string.IsNullOrWhiteSpace(repository.Name))
                {
                    continue;
                }

                repositories.Add(new GitHubInstallationRepository(
                    repository.Owner.Login,
                    repository.Name,
                    repository.FullName,
                    repository.Private));
            }

            if (payload.Repositories.Count < 100)
            {
                break;
            }

            page++;
        }

        return repositories
            .OrderBy(repository => repository.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal async Task<GitHubAuthSession> RefreshSessionAsync(GitHubAuthSession currentSession, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentSession);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSession.RefreshToken);
        EnsureSignInConfiguration();

        var client = _httpClientFactory.CreateClient(GitHubAuthClientName);
        var tokenResponse = await ExchangeRefreshTokenForAccessTokenAsync(client, currentSession.RefreshToken, cancellationToken).ConfigureAwait(false);

        var refreshToken = !string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
            ? tokenResponse.RefreshToken
            : currentSession.RefreshToken;

        return new GitHubAuthSession(
            currentSession.OwnerLogin,
            tokenResponse.AccessToken,
            currentSession.InstallationId,
            ComputeExpiresAtUtc(tokenResponse.ExpiresInSeconds),
            refreshToken,
            ComputeExpiresAtUtc(tokenResponse.RefreshTokenExpiresInSeconds));
    }

    internal ClaimsPrincipal CreatePrincipal(GitHubAuthSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.OwnerLogin),
            new(ClaimTypes.Name, session.OwnerLogin),
            new(_authOptions.HostedOwnerLoginClaimType, session.OwnerLogin),
            new(_authOptions.HostedAccessTokenClaimType, session.AccessToken),
        };

        if (!string.IsNullOrWhiteSpace(_authOptions.HostedInstallationIdClaimType) && session.InstallationId is { } installationId)
        {
            claims.Add(new Claim(_authOptions.HostedInstallationIdClaimType, installationId.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(_authOptions.HostedTokenExpiresAtClaimType) && session.TokenExpiresAtUtc is { } expiresAtUtc)
        {
            claims.Add(new Claim(_authOptions.HostedTokenExpiresAtClaimType, expiresAtUtc.ToString("O")));
        }

        if (!string.IsNullOrWhiteSpace(_authOptions.HostedRefreshTokenClaimType) && !string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            claims.Add(new Claim(_authOptions.HostedRefreshTokenClaimType, session.RefreshToken));
        }

        if (!string.IsNullOrWhiteSpace(_authOptions.HostedRefreshTokenExpiresAtClaimType) && session.RefreshTokenExpiresAtUtc is { } refreshTokenExpiresAtUtc)
        {
            claims.Add(new Claim(_authOptions.HostedRefreshTokenExpiresAtClaimType, refreshTokenExpiresAtUtc.ToString("O")));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    private async Task<AccessTokenResponseDto> ExchangeCodeForAccessTokenAsync(HttpClient client, string code, CancellationToken cancellationToken)
    {
        using var request = CreateTokenEndpointRequest(new Dictionary<string, string> { ["code"] = code });
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccessStatusCode(response);

        var payload = await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>(JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("GitHub sign-in failed because the access-token response was empty.");

        if (!string.IsNullOrWhiteSpace(payload.Error) || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("GitHub sign-in failed because GitHub did not return a valid access token.");
        }

        return payload;
    }

    private async Task<AccessTokenResponseDto> ExchangeRefreshTokenForAccessTokenAsync(HttpClient client, string refreshToken, CancellationToken cancellationToken)
    {
        using var request = CreateTokenEndpointRequest(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        });

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccessStatusCode(response);

        var payload = await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>(JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("GitHub token refresh failed because the access-token response was empty.");

        if (!string.IsNullOrWhiteSpace(payload.Error) || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("GitHub token refresh failed because GitHub did not return a valid access token.");
        }

        return payload;
    }

    private HttpRequestMessage CreateTokenEndpointRequest(Dictionary<string, string> extraFields)
    {
        var formFields = new Dictionary<string, string>
        {
            ["client_id"] = _authOptions.HostedGitHubAppClientId,
            ["client_secret"] = _authOptions.HostedGitHubAppClientSecret,
        };

        foreach (var field in extraFields)
        {
            formFields[field.Key] = field.Value;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, _authOptions.HostedGitHubAccessTokenEndpoint)
        {
            Content = new FormUrlEncodedContent(formFields),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static async Task<AuthenticatedUserDto> GetAuthenticatedUserAsync(HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        using var request = CreateGitHubApiRequest(HttpMethod.Get, "/user", accessToken);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccessStatusCode(response);

        var user = await response.Content.ReadFromJsonAsync<AuthenticatedUserDto>(JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("GitHub sign-in failed because the user response was empty.");

        if (string.IsNullOrWhiteSpace(user.Login))
        {
            throw new InvalidOperationException("GitHub sign-in failed because the user response did not include a login.");
        }

        return user;
    }

    private static async Task<long?> ResolveInstallationIdAsync(HttpClient client, string accessToken, string ownerLogin, CancellationToken cancellationToken)
    {
        using var request = CreateGitHubApiRequest(HttpMethod.Get, "/user/installations", accessToken);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccessStatusCode(response);

        var installations = await response.Content.ReadFromJsonAsync<UserInstallationsResponseDto>(JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("GitHub sign-in failed because the installations response was empty.");

        if (installations.Installations.Count == 0)
        {
            return null;
        }

        var matchingInstallation = installations.Installations
            .FirstOrDefault(installation => string.Equals(installation.Account?.Login, ownerLogin, StringComparison.OrdinalIgnoreCase));

        return matchingInstallation?.Id ?? installations.Installations[0].Id;
    }

    private static HttpRequestMessage CreateGitHubApiRequest(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private void EnsureSignInConfiguration()
    {
        if (!_authOptions.IsConfigured)
        {
            throw new InvalidOperationException(
                "GitHub sign-in is enabled but client credentials are missing. Configure HostedGitHubAppClientId and HostedGitHubAppClientSecret.");
        }
    }

    private static DateTimeOffset? ComputeExpiresAtUtc(long? expiresInSeconds)
    {
        if (expiresInSeconds is > 0)
        {
            return DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds.Value);
        }

        return null;
    }

    private static void EnsureSuccessStatusCode(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new HttpRequestException(
            $"GitHub sign-in request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            null,
            response.StatusCode);
    }

    private sealed class AccessTokenResponseDto
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public long? ExpiresInSeconds { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("refresh_token_expires_in")]
        public long? RefreshTokenExpiresInSeconds { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private sealed class AuthenticatedUserDto
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;
    }

    private sealed class UserInstallationsResponseDto
    {
        [JsonPropertyName("installations")]
        public List<InstallationDto> Installations { get; set; } = [];
    }

    private sealed class InstallationRepositoriesResponseDto
    {
        [JsonPropertyName("repositories")]
        public List<InstallationRepositoryDto> Repositories { get; set; } = [];
    }

    private sealed class InstallationRepositoryDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("private")]
        public bool Private { get; set; }

        [JsonPropertyName("owner")]
        public InstallationAccountDto? Owner { get; set; }
    }

    private sealed class InstallationDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("account")]
        public InstallationAccountDto? Account { get; set; }
    }

    private sealed class InstallationAccountDto
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;
    }
}

/// <summary>
/// A repository accessible through a GitHub App installation.
/// </summary>
public sealed record GitHubInstallationRepository(
    string OwnerLogin,
    string RepoName,
    string FullName,
    bool IsPrivate);
