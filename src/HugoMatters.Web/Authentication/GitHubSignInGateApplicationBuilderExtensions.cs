namespace HugoMatters.Web.Authentication;

public static class GitHubSignInGateApplicationBuilderExtensions
{
    public static IApplicationBuilder UseGitHubSignInGate(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<GitHubSignInGateMiddleware>();
    }
}
