using HugoMatter.Core.Models;
using HugoMatter.Core.Ports;
using HugoMatter.ThemePacks;
using NSubstitute;

namespace HugoMatter.Core.Tests;

internal static class TestHelpers
{
    public static ConnectedSite CreateConnectedSite(
        Guid? id = null,
        string themePackId = "hugo-profile",
        SiteStatus status = SiteStatus.Connected)
    {
        return new ConnectedSite
        {
            Id = id ?? Guid.NewGuid(),
            InstallationId = 42,
            OwnerLogin = "owner",
            RepoName = "hugo-site",
            DefaultBranch = "main",
            ThemePackId = themePackId,
            ConnectedAt = DateTimeOffset.UtcNow,
            Status = status,
        };
    }

    public static EditingSession CreateActiveSession(Guid siteId, bool hasUnsavedLocalEdits = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new EditingSession
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            BranchName = "hugo-matter/session-test1234",
            PullRequestNumber = 7,
            PullRequestUrl = "https://github.com/owner/hugo-site/pull/7",
            BaseBranch = "main",
            State = SessionState.Active,
            HasUnsavedLocalEdits = hasUnsavedLocalEdits,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public static ThemePackDefinition GetHugoProfilePack() =>
        new ThemePackRegistry().GetPackDetail("hugo-profile")
        ?? throw new InvalidOperationException("Hugo Profile pack was not found.");

    public static void SetupActiveContext(
        IMetadataStore metadataStore,
        IThemePackRegistry themePackRegistry,
        ConnectedSite? site = null,
        EditingSession? session = null)
    {
        site ??= CreateConnectedSite();
        session ??= CreateActiveSession(site.Id);

        metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns(session);

        var pack = themePackRegistry.GetPackDetail(site.ThemePackId) ?? GetHugoProfilePack();
        themePackRegistry.GetPackDetail(site.ThemePackId).Returns(pack);
        themePackRegistry.GetPackDetail(Arg.Is<string>(id => id != site.ThemePackId)).Returns((ThemePackDefinition?)null);
    }

    public static SessionContentBuffer CreateEmptyBuffer(Guid sessionId) =>
        new() { SessionId = sessionId };
}
