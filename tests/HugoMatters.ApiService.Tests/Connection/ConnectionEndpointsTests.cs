using System.Net;
using System.Net.Http.Json;
using HugoMatters.Core.Api;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using NSubstitute;

namespace HugoMatters.ApiService.Tests.Connection;

/// <summary>
/// Tests for connection endpoints.
/// </summary>
public sealed class ConnectionEndpointsTests
{
    /// <summary>
    /// GET /api/connection returns 404 when no site is connected.
    /// </summary>
    [Fact]
    public async Task GetConnection_ReturnsNotFound_WhenNoSiteConnected()
    {
        await using var factory = new HugoMattersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/connection", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// POST /api/connection/authorize connects a site when installation details are provided.
    /// </summary>
    [Fact]
    public async Task Authorize_ReturnsConnectedSite_WhenInstallationProvided()
    {
        await using var factory = new HugoMattersApiFactory();
        factory.GitHub.GetRepositoryAsync(42, "owner", "repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryInfo
            {
                OwnerLogin = "owner",
                RepoName = "repo",
                DefaultBranch = "main",
                HtmlUrl = "https://github.com/owner/repo",
            });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/connection/authorize", new AuthorizeRequest
        {
            InstallationId = 42,
            Owner = "owner",
            Repo = "repo",
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthorizeResponse>(HugoMattersApiFactory.JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(payload);
        Assert.Equal("connected", payload.Status);
        Assert.NotNull(payload.Site);
        Assert.Equal("owner", payload.Site.OwnerLogin);
        Assert.Equal("repo", payload.Site.RepoName);

        var getResponse = await client.GetAsync("/api/connection", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var site = await getResponse.Content.ReadFromJsonAsync<ConnectedSite>(HugoMattersApiFactory.JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(site);
        Assert.Equal(payload.Site.Id, site.Id);
    }

    /// <summary>
    /// DELETE /api/connection disconnects the bound site.
    /// </summary>
    [Fact]
    public async Task Disconnect_ReturnsNoContent_WhenSiteConnected()
    {
        await using var factory = new HugoMattersApiFactory();
        factory.GitHub.GetRepositoryAsync(42, "owner", "repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryInfo
            {
                OwnerLogin = "owner",
                RepoName = "repo",
                DefaultBranch = "main",
            });

        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/connection/authorize", new AuthorizeRequest { InstallationId = 42, Owner = "owner", Repo = "repo" }, cancellationToken: TestContext.Current.CancellationToken);

        var response = await client.DeleteAsync("/api/connection", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync("/api/connection", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    /// <summary>
    /// POST /api/connection/authorize returns a clear error when GitHub App is not configured.
    /// </summary>
    [Fact]
    public async Task Authorize_ReturnsGitHubAppNotConfigured_WhenCredentialsMissing()
    {
        await using var factory = new HugoMattersApiFactory { IncludeGitHubAppClientId = false };
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/connection/authorize",
            new AuthorizeRequest { Owner = "owner", Repo = "repo" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorBody>(
            HugoMattersApiFactory.JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(error);
        Assert.Equal("github_app_not_configured", error.Code);
        Assert.Contains("GitHub App is not configured", error.Message);
    }

    /// <summary>
    /// POST /api/connection/authorize returns redirect when installation is not provided.
    /// </summary>
    [Fact]
    public async Task Authorize_ReturnsRedirect_WhenInstallationMissing()
    {
        await using var factory = new HugoMattersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/connection/authorize", new AuthorizeRequest(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AuthorizeResponse>(HugoMattersApiFactory.JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(payload);
        Assert.Equal("redirect", payload.Status);
        Assert.False(string.IsNullOrWhiteSpace(payload.RedirectUrl));
    }
}
