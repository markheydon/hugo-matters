using System.Net;
using System.Net.Http.Json;
using HugoMatters.Web.Authentication;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HugoMatters.Web.Tests;

public sealed class GitHubAuthGatewayTests
{
    [Fact]
    public void CreatePrincipal_maps_owner_login_and_installation_id()
    {
        var gateway = CreateGateway([]);
        var session = new GitHubAuthSession(
            "octocat",
            "access-token",
            42,
            DateTimeOffset.UtcNow.AddHours(1),
            "refresh-token",
            DateTimeOffset.UtcNow.AddDays(1));

        var principal = gateway.CreatePrincipal(session);

        Assert.Equal("octocat", principal.FindFirst(GitHubAuthClaimTypes.OwnerLogin)?.Value);
        Assert.Equal("42", principal.FindFirst(GitHubAuthClaimTypes.InstallationId)?.Value);
        Assert.Equal("access-token", principal.FindFirst(GitHubAuthClaimTypes.AccessToken)?.Value);
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
        var session = await gateway.ExchangeCodeForSessionAsync("oauth-code", CancellationToken.None);

        Assert.Equal("octocat", session.OwnerLogin);
        Assert.Equal("user-token", session.AccessToken);
        Assert.Equal(99, session.InstallationId);
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
}
