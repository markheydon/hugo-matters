using System.Net;
using System.Net.Http.Json;
using HugoMatter.Core.Api;
using HugoMatter.Core.Models;
using HugoMatter.Core.Ports;
using NSubstitute;

namespace HugoMatter.ApiService.Tests.Session;

/// <summary>
/// Tests for session lifecycle endpoints.
/// </summary>
public sealed class SessionEndpointsTests
{
    /// <summary>
    /// GET /api/session returns 404 when no active session exists.
    /// </summary>
    [Fact]
    public async Task GetSession_ReturnsNotFound_WhenNoActiveSession()
    {
        await using var factory = CreateConnectedFactory();
        using var client = factory.CreateClient();
        await ConnectSite(client);

        var response = await client.GetAsync("/api/session");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// POST /api/session creates a session with branch and pull request metadata.
    /// </summary>
    [Fact]
    public async Task StartSession_ReturnsCreatedSession()
    {
        await using var factory = CreateConnectedFactory();
        ConfigureSessionGitHub(factory);

        using var client = factory.CreateClient();
        await ConnectSite(client);
        var response = await client.PostAsync("/api/session", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<EditingSession>(HugoMatterApiFactory.JsonOptions);
        Assert.NotNull(session);
        Assert.Equal(SessionState.Active, session.State);
        Assert.False(session.HasUnsavedLocalEdits);
        Assert.True(session.PullRequestNumber > 0);
        Assert.StartsWith("hugo-matter/session-", session.BranchName);
    }

    /// <summary>
    /// POST /api/session returns conflict when an active session already exists.
    /// </summary>
    [Fact]
    public async Task StartSession_ReturnsConflict_WhenSessionAlreadyActive()
    {
        await using var factory = CreateConnectedFactory();
        ConfigureSessionGitHub(factory);

        using var client = factory.CreateClient();
        await ConnectSite(client);
        await client.PostAsync("/api/session", null);

        var response = await client.PostAsync("/api/session", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<EditingSession>(HugoMatterApiFactory.JsonOptions);
        Assert.NotNull(session);
        Assert.Equal(SessionState.Active, session.State);
    }

    private static HugoMatterApiFactory CreateConnectedFactory()
    {
        var factory = new HugoMatterApiFactory();
        factory.GitHub.GetRepositoryAsync(42, "owner", "repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryInfo
            {
                OwnerLogin = "owner",
                RepoName = "repo",
                DefaultBranch = "main",
            });
        return factory;
    }

    private static void ConfigureSessionGitHub(HugoMatterApiFactory factory)
    {
        factory.GitHub.CreateBranchAsync(
            42,
            "owner",
            "repo",
            Arg.Any<string>(),
            "main",
            Arg.Any<CancellationToken>())
            .Returns("branch-sha");

        factory.GitHub.CreatePullRequestAsync(
            42,
            "owner",
            "repo",
            Arg.Any<string>(),
            Arg.Any<string>(),
            "main",
            Arg.Any<CancellationToken>())
            .Returns(new GitHubPullRequestInfo
            {
                Number = 7,
                HtmlUrl = "https://github.com/owner/repo/pull/7",
            });
    }

    private static async Task ConnectSite(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/connection/authorize",
            new AuthorizeRequest { InstallationId = 42, Owner = "owner", Repo = "repo" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
