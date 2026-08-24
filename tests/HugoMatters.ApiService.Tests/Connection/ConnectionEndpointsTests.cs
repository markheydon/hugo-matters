using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    /// POST /api/connection connects a site when installation details are provided.
    /// </summary>
    [Fact]
    public async Task Connect_ReturnsConnectedSite_WhenInstallationProvided()
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
        var response = await client.PostAsJsonAsync("/api/connection", new ConnectRequest
        {
            InstallationId = 42,
            Owner = "owner",
            Repo = "repo",
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var site = await response.Content.ReadFromJsonAsync<ConnectedSite>(HugoMattersApiFactory.JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(site);
        Assert.Equal("owner", site.OwnerLogin);
        Assert.Equal("repo", site.RepoName);

        var getResponse = await client.GetAsync("/api/connection", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var persisted = await getResponse.Content.ReadFromJsonAsync<ConnectedSite>(HugoMattersApiFactory.JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal(site.Id, persisted.Id);
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
        await client.PostAsJsonAsync("/api/connection", new ConnectRequest { InstallationId = 42, Owner = "owner", Repo = "repo" }, cancellationToken: TestContext.Current.CancellationToken);

        var response = await client.DeleteAsync("/api/connection", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync("/api/connection", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    /// <summary>
    /// POST /api/connection returns a clear error when GitHub App is not configured.
    /// </summary>
    [Fact]
    public async Task Connect_ReturnsGitHubAppNotConfigured_WhenCredentialsMissing()
    {
        await using var factory = new HugoMattersApiFactory { IncludeGitHubAppCredentials = false };
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/connection",
            new ConnectRequest { InstallationId = 42, Owner = "owner", Repo = "repo" },
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
    /// POST /api/connection returns validation error when owner/repo missing.
    /// </summary>
    [Fact]
    public async Task Connect_ReturnsBadRequest_WhenOwnerRepoMissing()
    {
        await using var factory = new HugoMattersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/connection",
            new ConnectRequest { InstallationId = 42, Owner = "", Repo = "repo" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Connect_ReturnsPullRequestsDisabled_WhenRepoFeatureOff()
    {
        await using var factory = new HugoMattersApiFactory();
        factory.GitHub.GetRepositoryAsync(42, "owner", "repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryInfo
            {
                OwnerLogin = "owner",
                RepoName = "repo",
                DefaultBranch = "main",
                HasPullRequests = false,
            });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/connection",
            new ConnectRequest { InstallationId = 42, Owner = "owner", Repo = "repo" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>(
            HugoMattersApiFactory.JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(error);
        Assert.Equal("pull_requests_disabled", error.Code);
        Assert.Contains("Pull requests are disabled", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReadiness_ReturnsNotReady_WhenPullRequestsDisabled()
    {
        await using var factory = new HugoMattersApiFactory();
        factory.GitHub.GetRepositoryAsync(42, "owner", "repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryInfo
            {
                OwnerLogin = "owner",
                RepoName = "repo",
                DefaultBranch = "main",
                HasPullRequests = true,
            });

        using var client = factory.CreateClient();
        var connectResponse = await client.PostAsJsonAsync(
            "/api/connection",
            new ConnectRequest { InstallationId = 42, Owner = "owner", Repo = "repo" },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, connectResponse.StatusCode);

        factory.GitHub.GetRepositoryAsync(42, "owner", "repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryInfo
            {
                OwnerLogin = "owner",
                RepoName = "repo",
                DefaultBranch = "main",
                HasPullRequests = false,
            });

        var readinessResponse = await client.GetAsync("/api/connection/readiness", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
        var doc = await readinessResponse.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(doc.GetProperty("ready").GetBoolean());
        Assert.Equal("pull_requests_disabled", doc.GetProperty("code").GetString());
    }
}
