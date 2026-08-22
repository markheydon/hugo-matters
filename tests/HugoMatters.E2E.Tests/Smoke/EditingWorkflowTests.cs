using Microsoft.Playwright;

namespace HugoMatters.E2E.Tests.Smoke;

/// <summary>
/// Thin smoke test for the connect page shell. Full connect→edit→save requires GitHub App credentials.
/// </summary>
public sealed class EditingWorkflowTests
{
    [Fact]
    public async Task Connect_page_loads_when_web_is_running()
    {
        var baseUrl = Environment.GetEnvironmentVariable("HUGO_MATTERS_WEB_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            // Skip when no running app URL is provided (CI/local without Aspire).
            return;
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/connect");
        var heading = await page.TextContentAsync("h1");
        Assert.NotNull(heading);
        Assert.Contains("Connect", heading, StringComparison.OrdinalIgnoreCase);
    }
}
