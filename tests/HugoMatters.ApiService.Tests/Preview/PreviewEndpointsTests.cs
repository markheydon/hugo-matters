using System.Net;
using System.Net.Http.Json;
using HugoMatters.Core.Api;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using NSubstitute;

namespace HugoMatters.ApiService.Tests.Preview;

/// <summary>
/// Tests for site preview API endpoints.
/// </summary>
public sealed class PreviewEndpointsTests
{
    [Fact]
    public async Task StartSitePreview_ReturnsOk_WhenPreviewAlreadyStarting()
    {
        await using var factory = CreateConnectedFactory();
        ConfigureSessionGitHub(factory);

        var session = await StartSessionAsync(factory);
        var existingPreview = new SitePreviewInfo
        {
            Id = $"preview-{session.Id:N}",
            SessionId = session.Id,
            Status = SitePreviewState.Starting,
            SourceRef = "abc1234",
        };

        factory.PreviewOrchestrator.GetStatusAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(existingPreview);

        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/preview/site", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SitePreviewResponse>(
            HugoMattersApiFactory.JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal("Starting", body.Status);
        await factory.PreviewOrchestrator.DidNotReceive()
            .StartPreviewAsync(Arg.Any<EditingSession>(), Arg.Any<ConnectedSite>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static HugoMattersApiFactory CreateConnectedFactory()
    {
        var factory = new HugoMattersApiFactory();
        factory.GitHub.GetRepositoryAsync(42, "owner", "repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryInfo
            {
                OwnerLogin = "owner",
                RepoName = "repo",
                DefaultBranch = "main",
            });
        return factory;
    }

    private static void ConfigureSessionGitHub(HugoMattersApiFactory factory)
    {
        factory.GitHub.CreateBranchAsync(
                42,
                "owner",
                "repo",
                Arg.Any<string>(),
                "main",
                Arg.Any<CancellationToken>())
            .Returns("branch-sha");

        factory.GitHub.CreateCommitAsync(
                42,
                "owner",
                "repo",
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<GitHubFileChange>>(),
                Arg.Any<CancellationToken>())
            .Returns(new GitHubCommitInfo { Sha = "commit-sha" });

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

    private static async Task<EditingSession> StartSessionAsync(HugoMattersApiFactory factory)
    {
        using var client = factory.CreateClient();
        await ConnectSite(client);
        var response = await client.PostAsync("/api/session", null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EditingSession>(
            HugoMattersApiFactory.JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken))!;
    }

    private static async Task ConnectSite(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/connection",
            new ConnectRequest
            {
                InstallationId = 42,
                Owner = "owner",
                Repo = "repo",
            },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
