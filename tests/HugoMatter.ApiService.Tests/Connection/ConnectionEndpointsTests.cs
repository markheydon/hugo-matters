using System.Net;
using System.Net.Http.Json;
using HugoMatter.Core.Api;
using HugoMatter.Core.Models;
using HugoMatter.Core.Ports;
using NSubstitute;

namespace HugoMatter.ApiService.Tests.Connection;

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
        await using var factory = new HugoMatterApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/connection");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// POST /api/connection/authorize connects a site when installation details are provided.
    /// </summary>
    [Fact]
    public async Task Authorize_ReturnsConnectedSite_WhenInstallationProvided()
    {
        await using var factory = new HugoMatterApiFactory();
        factory.GitHub.GetRepositoryAsync(42, "owner", "repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryInfo
            {
                OwnerLogin = "owner",
                RepoName = "repo",
                DefaultBranch = "main",
                HtmlUrl = "https://github.com/owner/repo",
            });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/connection/authorize",
            new AuthorizeRequest
            {
                InstallationId = 42,
                Owner = "owner",
                Repo = "repo",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthorizeResponse>(HugoMatterApiFactory.JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("connected", payload.Status);
        Assert.NotNull(payload.Site);
        Assert.Equal("owner", payload.Site.OwnerLogin);
        Assert.Equal("repo", payload.Site.RepoName);

        var getResponse = await client.GetAsync("/api/connection");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var site = await getResponse.Content.ReadFromJsonAsync<ConnectedSite>(HugoMatterApiFactory.JsonOptions);
        Assert.NotNull(site);
        Assert.Equal(payload.Site.Id, site.Id);
    }

    /// <summary>
    /// DELETE /api/connection disconnects the bound site.
    /// </summary>
    [Fact]
    public async Task Disconnect_ReturnsNoContent_WhenSiteConnected()
    {
        await using var factory = new HugoMatterApiFactory();
        factory.GitHub.GetRepositoryAsync(42, "owner", "repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryInfo
            {
                OwnerLogin = "owner",
                RepoName = "repo",
                DefaultBranch = "main",
            });

        using var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/connection/authorize",
            new AuthorizeRequest { InstallationId = 42, Owner = "owner", Repo = "repo" });

        var response = await client.DeleteAsync("/api/connection");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync("/api/connection");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    /// <summary>
    /// POST /api/connection/authorize returns redirect when installation is not provided.
    /// </summary>
    [Fact]
    public async Task Authorize_ReturnsRedirect_WhenInstallationMissing()
    {
        await using var factory = new HugoMatterApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/connection/authorize", new AuthorizeRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AuthorizeResponse>(HugoMatterApiFactory.JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("redirect", payload.Status);
        Assert.False(string.IsNullOrWhiteSpace(payload.RedirectUrl));
    }
}
