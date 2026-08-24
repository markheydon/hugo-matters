using HugoMatters.Web.Authentication;

namespace HugoMatters.Web.Tests;

public sealed class GitHubAuthReturnUrlTests
{
    [Fact]
    public void GetSafeReturnUrl_rejects_open_redirects()
    {
        Assert.Equal("/", GitHubAuthReturnUrl.GetSafeReturnUrl(null));
        Assert.Equal("/", GitHubAuthReturnUrl.GetSafeReturnUrl(""));
        Assert.Equal("/", GitHubAuthReturnUrl.GetSafeReturnUrl("https://evil.test"));
        Assert.Equal("/", GitHubAuthReturnUrl.GetSafeReturnUrl("//evil.test/path"));
    }

    [Fact]
    public void GetSafeReturnUrl_allows_relative_paths()
    {
        Assert.Equal("/connect", GitHubAuthReturnUrl.GetSafeReturnUrl("/connect"));
        Assert.Equal("/session", GitHubAuthReturnUrl.GetSafeReturnUrl("/session"));
    }

    [Fact]
    public void GetRequestedReturnUrl_reads_returnUrl_query()
    {
        var uri = new Uri("https://localhost:7175/welcome?returnUrl=%2Fconnect");
        Assert.Equal("/connect", GitHubAuthReturnUrl.GetRequestedReturnUrl(uri));
    }
}
