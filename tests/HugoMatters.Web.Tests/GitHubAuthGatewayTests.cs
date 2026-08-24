using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using HugoMatters.Web.Authentication;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HugoMatters.Web.Tests;

public sealed class GitHubAuthGatewayTests
{
    [Fact]
    public void CreatePrincipal_maps_owner_login_and_session_key_without_tokens()
    {
        var gateway = CreateGateway([]);
        var session = new GitHubAuthSession(
            "octocat",
            "access-token",
            42,
            DateTimeOffset.UtcNow.AddHours(1),
            "refresh-token",
            DateTimeOffset.UtcNow.AddDays(1));

        var principal = gateway.CreatePrincipal(session, "session-key-abc");

        Assert.Equal("octocat", principal.FindFirst(GitHubAuthClaimTypes.OwnerLogin)?.Value);
        Assert.Equal("42", principal.FindFirst(GitHubAuthClaimTypes.InstallationId)?.Value);
        Assert.Equal("session-key-abc", principal.FindFirst(GitHubAuthClaimTypes.SessionKey)?.Value);
        Assert.Null(principal.FindFirst(GitHubAuthClaimTypes.AccessToken));
        Assert.Null(principal.FindFirst(GitHubAuthClaimTypes.RefreshToken));
    }

    [Fact]
    public async Task ExchangeCodeForSessionAsync_returns_session_with_installation_id()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(JsonResponse(new
        {
            access_token = "user-token",
            expires_in = 3600,
            refresh_token = "refresh",
            refresh_token_expires_in = 86400,
        }));
        responses.Enqueue(JsonResponse(new { login = "octocat" }));
        responses.Enqueue(JsonResponse(new
        {
            installations = new[]
            {
                new { id = 99, account = new { login = "octocat" } },
            },
        }));

        var gateway = CreateGateway(responses);
        var session = await gateway.ExchangeCodeForSessionAsync(
            "oauth-code",
            "https://localhost:7175/auth/callback",
            CancellationToken.None);

        Assert.Equal("octocat", session.OwnerLogin);
        Assert.Equal("user-token", session.AccessToken);
        Assert.Equal(99, session.InstallationId);
    }

    [Fact]
    public async Task ExchangeCodeForSessionAsync_sends_redirect_uri_on_token_exchange()
    {
        var handler = new CapturingHttpMessageHandler();
        handler.Responses.Enqueue(JsonResponse(new { access_token = "user-token", expires_in = 3600 }));
        handler.Responses.Enqueue(JsonResponse(new { login = "octocat" }));
        handler.Responses.Enqueue(JsonResponse(new
        {
            installations = new[] { new { id = 1, account = new { login = "octocat" } } },
        }));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(GitHubAuthGateway.GitHubAuthClientName).Returns(httpClient);

        var gateway = new GitHubAuthGateway(
            httpClientFactory,
            Options.Create(new GitHubAuthOptions
            {
                HostedGitHubAppClientId = "test-client",
                HostedGitHubAppClientSecret = "test-secret",
            }));

        await gateway.ExchangeCodeForSessionAsync(
            "oauth-code",
            "https://localhost:7175/auth/callback",
            CancellationToken.None);

        var requestIndex = handler.Requests.FindIndex(r =>
            r.RequestUri is not null
            && r.RequestUri.Host.Contains("github.com", StringComparison.Ordinal)
            && r.RequestUri.PathAndQuery.Contains("access_token", StringComparison.Ordinal));
        var body = handler.RequestBodies[requestIndex];
        Assert.Contains("redirect_uri=", body, StringComparison.Ordinal);
        Assert.Contains(
            Uri.EscapeDataString("https://localhost:7175/auth/callback"),
            body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExchangeCodeForSessionAsync_returns_null_installation_when_multiple_without_match()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(JsonResponse(new { access_token = "user-token", expires_in = 3600 }));
        responses.Enqueue(JsonResponse(new { login = "octocat" }));
        responses.Enqueue(JsonResponse(new
        {
            installations = new[]
            {
                new { id = 10, account = new { login = "my-org" } },
                new { id = 11, account = new { login = "other-org" } },
            },
        }));

        var gateway = CreateGateway(responses);
        var session = await gateway.ExchangeCodeForSessionAsync(
            "oauth-code",
            "https://localhost:7175/auth/callback",
            CancellationToken.None);

        Assert.Null(session.InstallationId);
    }

    [Fact]
    public void BuildAuthoriseUrl_includes_client_id_and_state()
    {
        var gateway = CreateGateway([]);
        var url = gateway.BuildAuthoriseUrl("state-value", "https://localhost:7175/auth/callback");

        Assert.Contains("client_id=test-client", url, StringComparison.Ordinal);
        Assert.Contains("state=state-value", url, StringComparison.Ordinal);
        Assert.Contains("redirect_uri=", url, StringComparison.Ordinal);
    }

    private static GitHubAuthGateway CreateGateway(Queue<HttpResponseMessage> responses)
    {
        var handler = new QueueHttpMessageHandler(responses);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(GitHubAuthGateway.GitHubAuthClientName).Returns(httpClient);

        var options = Options.Create(new GitHubAuthOptions
        {
            HostedGitHubAppClientId = "test-client",
            HostedGitHubAppClientSecret = "test-secret",
        });

        return new GitHubAuthGateway(httpClientFactory, options);
    }

    private static HttpResponseMessage JsonResponse(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payload),
        };

    private sealed class QueueHttpMessageHandler(Queue<HttpResponseMessage> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (responses.Count == 0)
            {
                throw new InvalidOperationException("No queued HTTP responses remain.");
            }

            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        internal List<HttpRequestMessage> Requests { get; } = [];

        internal List<string> RequestBodies { get; } = [];

        internal Queue<HttpResponseMessage> Responses { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is not null)
            {
                RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }
            else
            {
                RequestBodies.Add(string.Empty);
            }

            if (Responses.Count == 0)
            {
                throw new InvalidOperationException("No queued HTTP responses remain.");
            }

            return Responses.Dequeue();
        }
    }
}
