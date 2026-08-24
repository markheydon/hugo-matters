using Microsoft.Playwright;

namespace HugoMatters.E2E.Tests.Smoke;

/// <summary>
/// Thin smoke tests for auth gate and connect page shell.
/// </summary>
public sealed class EditingWorkflowTests
{
    [Fact]
    public async Task Root_redirects_to_welcome_when_web_is_running()
    {
        var baseUrl = Environment.GetEnvironmentVariable("HUGO_MATTERS_WEB_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/");

        var signInButton = page.Locator("a", new PageLocatorOptions { HasTextString = "Sign in with GitHub" });
        await signInButton.WaitForAsync();
        Assert.True(await signInButton.IsVisibleAsync());
    }

    [Fact]
    public async Task Connect_requires_auth_when_web_is_running()
    {
        var baseUrl = Environment.GetEnvironmentVariable("HUGO_MATTERS_WEB_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/connect");

        var signInButton = page.Locator("a", new PageLocatorOptions { HasTextString = "Sign in with GitHub" });
        await signInButton.WaitForAsync();
        Assert.True(await signInButton.IsVisibleAsync());
    }
}
