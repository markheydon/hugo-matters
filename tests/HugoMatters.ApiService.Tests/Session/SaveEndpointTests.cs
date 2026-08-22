using System.Net;
using System.Net.Http.Json;
using HugoMatters.Core.Api;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using NSubstitute;

namespace HugoMatters.ApiService.Tests.Session;

/// <summary>
/// Tests for the session save endpoint.
/// </summary>
public sealed class SaveEndpointTests
{
    /// <summary>
    /// POST /api/session/save commits buffered content and clears unsaved edits.
    /// </summary>
    [Fact]
    public async Task Save_ReturnsCommitSha_AndClearsUnsavedEdits()
    {
        await using var factory = new HugoMattersApiFactory();
        ConfigureGitHub(factory);

        using var client = factory.CreateClient();
        await ConnectSite(client);
        await client.PostAsync("/api/session", null);

        var createResponse = await client.PostAsJsonAsync(
            "/api/content",
            new ContentCreateRequest
            {
                ContentType = ContentTypeKind.Post,
                Slug = "hello-world",
                Title = "Hello",
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var saveResponse = await client.PostAsJsonAsync(
            "/api/session/save",
            new SaveRequest { CommitMessage = "Save test post" });

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var saveResult = await saveResponse.Content.ReadFromJsonAsync<SaveResult>(HugoMattersApiFactory.JsonOptions);
        Assert.NotNull(saveResult);
        Assert.Equal("commit-sha", saveResult.CommitSha);
        Assert.False(saveResult.HasUnsavedLocalEdits);

        var sessionResponse = await client.GetAsync("/api/session");
        var session = await sessionResponse.Content.ReadFromJsonAsync<EditingSession>(HugoMattersApiFactory.JsonOptions);
        Assert.NotNull(session);
        Assert.False(session.HasUnsavedLocalEdits);
    }

    private static void ConfigureGitHub(HugoMattersApiFactory factory)
    {
        factory.GitHub.GetRepositoryAsync(42, "owner", "repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryInfo
            {
                OwnerLogin = "owner",
                RepoName = "repo",
                DefaultBranch = "main",
            });

        factory.GitHub.CreateBranchAsync(
            42, "owner", "repo", Arg.Any<string>(), "main", Arg.Any<CancellationToken>())
            .Returns("branch-sha");

        factory.GitHub.CreatePullRequestAsync(
            42, "owner", "repo", Arg.Any<string>(), Arg.Any<string>(), "main", Arg.Any<CancellationToken>())
            .Returns(new GitHubPullRequestInfo { Number = 3, HtmlUrl = "https://github.com/owner/repo/pull/3" });

        factory.GitHub.GetBranchTipShaAsync(
            42, "owner", "repo", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("tip-sha");

        factory.GitHub.ListTreeAsync(
            42, "owner", "repo", "tip-sha", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<GitHubTreeEntry>());

        factory.GitHub.CreateCommitAsync(
            42,
            "owner",
            "repo",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<GitHubFileChange>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GitHubCommitInfo { Sha = "commit-sha" });
    }

    private static async Task ConnectSite(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/connection/authorize",
            new AuthorizeRequest { InstallationId = 42, Owner = "owner", Repo = "repo" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
