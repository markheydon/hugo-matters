using HugoMatters.Web.Authentication;

namespace HugoMatters.Web.Tests;

public sealed class GitHubAuthTokenStoreTests
{
    [Fact]
    public void StoreSession_round_trips_session_data()
    {
        var store = new GitHubAuthTokenStore();
        var session = new GitHubAuthSession(
            "octocat",
            "access-token",
            42,
            DateTimeOffset.UtcNow.AddHours(1),
            "refresh",
            DateTimeOffset.UtcNow.AddDays(1));

        var key = store.StoreSession(session);
        var loaded = store.TryGetSession(key);

        Assert.NotNull(loaded);
        Assert.Equal("octocat", loaded.OwnerLogin);
        Assert.Equal("access-token", loaded.AccessToken);
        Assert.Equal(42, loaded.InstallationId);
    }

    [Fact]
    public void RemoveSession_deletes_stored_session()
    {
        var store = new GitHubAuthTokenStore();
        var key = store.StoreSession(new GitHubAuthSession("octocat", "token", 1, null, "", null));

        store.RemoveSession(key);

        Assert.Null(store.TryGetSession(key));
    }
}
