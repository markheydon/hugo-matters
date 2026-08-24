using System.Security.Cryptography;
using System.Text;
using HugoMatters.Web;
using HugoMatters.Web.Authentication;
using HugoMatters.Web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

const string SignInStateCookieName = "hugo-matters.github-sign-in-state";

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var callbackBaseUri = builder.Configuration.GetSection(GitHubAuthOptions.SectionName)
    .GetValue<string>(nameof(GitHubAuthOptions.HostedSignInCallbackBaseUri));

if (TryGetConfiguredHttpsPort(callbackBaseUri, out var httpsPort))
{
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = httpsPort);
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();
builder.Services.AddCascadingAuthenticationState();

builder.Services.Configure<GitHubAuthOptions>(
    builder.Configuration.GetSection(GitHubAuthOptions.SectionName));

builder.Services.AddHttpClient<HugoMattersApiClient>(client =>
    client.BaseAddress = new("https+http://apiservice"));

builder.Services.AddHttpClient(GitHubAuthGateway.GitHubAuthClientName, client =>
{
    client.BaseAddress = new Uri("https://api.github.com");
    // GitHub rejects API calls without a User-Agent (403 Forbidden).
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HugoMatters");
});

builder.Services.AddScoped<GitHubAuthGateway>();
builder.Services.AddScoped<GitHubUserContext>();

var hostedAuthOptions = builder.Configuration.GetSection(GitHubAuthOptions.SectionName).Get<GitHubAuthOptions>()
    ?? new GitHubAuthOptions();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/welcome";
        options.LogoutPath = "/auth/sign-out";
        options.Events = GitHubCookieAuthenticationEvents.Create(hostedAuthOptions);
    });

// Do not set FallbackPolicy: it challenges static assets (/_framework, /css) before Blazor can start.
// Page protection uses AuthorizeRouteView; HTTP protection uses GitHubSignInGateMiddleware.
builder.Services.AddAuthorization();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseGitHubSignInGate();
app.UseAntiforgery();
app.UseOutputCache();
app.MapStaticAssets();

app.MapGet("/auth/error", static (HttpContext context) =>
{
    var reason = context.Request.Query["reason"].ToString();
    var page = GitHubAuthErrorPageRenderer.Render(context, reason);
    return Results.Content(page.Html, "text/html; charset=utf-8", statusCode: page.StatusCode);
}).AllowAnonymous();

app.MapGet("/auth/sign-in", static (HttpContext context, GitHubAuthGateway authGateway, IOptions<GitHubAuthOptions> optionsAccessor) =>
{
    var returnUrl = GitHubAuthReturnUrl.GetSafeReturnUrl(GetReturnUrlFromQuery(context.Request.Query));
    var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
    var options = optionsAccessor.Value;
    var callbackUri = BuildCallbackUri(context, options);
    var authoriseUrl = authGateway.BuildAuthoriseUrl(state, callbackUri);

    context.Response.Cookies.Append(
        SignInStateCookieName,
        BuildStatePayload(state, returnUrl),
        new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Path = "/",
            SameSite = SameSiteMode.None,
            Secure = true,
            MaxAge = TimeSpan.FromMinutes(10),
        });

    return Results.Redirect(authoriseUrl);
}).AllowAnonymous();

app.MapGet("/auth/callback", static async (
    HttpContext context,
    GitHubAuthGateway authGateway,
    ILogger<Program> logger) =>
{
    if (!TryReadAndClearStateCookie(context, out var state, out var returnUrl))
    {
        return Results.Redirect(GitHubAuthErrorRoutes.BuildErrorUrl(GitHubAuthErrorRoutes.SignInStateInvalid));
    }

    var returnedState = context.Request.Query["state"].ToString();
    if (!string.Equals(state, returnedState, StringComparison.Ordinal))
    {
        return Results.Redirect(GitHubAuthErrorRoutes.BuildErrorUrl(GitHubAuthErrorRoutes.SignInStateInvalid));
    }

    var signInError = context.Request.Query["error"].ToString();
    if (!string.IsNullOrWhiteSpace(signInError))
    {
        return Results.Redirect(GitHubAuthErrorRoutes.BuildErrorUrl(GitHubAuthErrorRoutes.SignInDenied));
    }

    var code = context.Request.Query["code"].ToString();
    if (string.IsNullOrWhiteSpace(code))
    {
        return Results.Redirect(GitHubAuthErrorRoutes.BuildErrorUrl(GitHubAuthErrorRoutes.SignInIncomplete));
    }

    GitHubAuthSession session;
    try
    {
        session = await authGateway.ExchangeCodeForSessionAsync(code, context.RequestAborted).ConfigureAwait(false);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "GitHub sign-in failed while establishing a session.");
        return Results.Redirect(GitHubAuthErrorRoutes.BuildErrorUrl(GitHubAuthErrorRoutes.SignInFailed));
    }
    catch (HttpRequestException ex)
    {
        logger.LogWarning(ex, "GitHub sign-in failed because GitHub returned an unexpected HTTP response.");
        return Results.Redirect(GitHubAuthErrorRoutes.BuildErrorUrl(GitHubAuthErrorRoutes.SignInUnavailable));
    }

    var principal = authGateway.CreatePrincipal(session);
    var authenticationProperties = new AuthenticationProperties
    {
        IsPersistent = false,
        AllowRefresh = true,
        ExpiresUtc = session.TokenExpiresAtUtc,
    };

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        authenticationProperties).ConfigureAwait(false);

    if (session.InstallationId is null or <= 0)
    {
        return Results.Redirect("/connect?needs_install=true");
    }

    return Results.Redirect(returnUrl);
}).AllowAnonymous();

app.MapGet("/auth/sign-out", (Delegate)SignOutSession).AllowAnonymous();
app.MapPost("/auth/sign-out", (Delegate)SignOutSession).DisableAntiforgery().AllowAnonymous();

app.MapGet("/auth/session-expired", static async (HttpContext context) =>
{
    var returnUrl = GitHubAuthReturnUrl.GetSafeReturnUrl(GetReturnUrlFromQuery(context.Request.Query));
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
    return Results.Redirect(GitHubAuthErrorRoutes.BuildErrorUrl(GitHubAuthErrorRoutes.SessionExpired, returnUrl));
}).AllowAnonymous();

// AuthorizeRouteView handles page auth; do not RequireAuthorization() on Blazor infrastructure endpoints.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

app.Run();

static async Task<IResult> SignOutSession(HttpContext context)
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
    return Results.Redirect("/welcome");
}

static string BuildCallbackUri(HttpContext context, GitHubAuthOptions options)
{
    var callbackPath = options.HostedSignInCallbackPath;
    if (string.IsNullOrWhiteSpace(callbackPath))
    {
        callbackPath = "/auth/callback";
    }

    if (!callbackPath.StartsWith("/", StringComparison.Ordinal))
    {
        callbackPath = $"/{callbackPath}";
    }

    if (!string.IsNullOrWhiteSpace(options.HostedSignInCallbackBaseUri)
        && Uri.TryCreate(options.HostedSignInCallbackBaseUri, UriKind.Absolute, out var callbackBaseUri))
    {
        return new Uri(callbackBaseUri, callbackPath).ToString();
    }

    return $"{context.Request.Scheme}://{context.Request.Host}{callbackPath}";
}

static string GetReturnUrlFromQuery(IQueryCollection query)
{
    var returnUrl = query["returnUrl"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(returnUrl))
    {
        return GitHubAuthReturnUrl.GetSafeReturnUrl(returnUrl);
    }

    return GitHubAuthReturnUrl.GetSafeReturnUrl(query["ReturnUrl"].FirstOrDefault());
}

static string BuildStatePayload(string state, string returnUrl) =>
    WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes($"{state}|{returnUrl}"));

static bool TryReadAndClearStateCookie(HttpContext context, out string state, out string returnUrl)
{
    state = string.Empty;
    returnUrl = "/";

    if (!context.Request.Cookies.TryGetValue(SignInStateCookieName, out var payload) || string.IsNullOrWhiteSpace(payload))
    {
        return false;
    }

    DeleteSignInStateCookie(context);

    string decoded;
    try
    {
        decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(payload));
    }
    catch (FormatException)
    {
        return false;
    }

    var separatorIndex = decoded.IndexOf('|');
    if (separatorIndex <= 0 || separatorIndex == decoded.Length - 1)
    {
        return false;
    }

    state = decoded[..separatorIndex];
    returnUrl = GitHubAuthReturnUrl.GetSafeReturnUrl(decoded[(separatorIndex + 1)..]);
    return true;
}

static void DeleteSignInStateCookie(HttpContext context)
{
    context.Response.Cookies.Delete(
        SignInStateCookieName,
        new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.None,
        });
}

static bool TryGetConfiguredHttpsPort(string? callbackBaseUri, out int httpsPort)
{
    httpsPort = 0;

    if (!Uri.TryCreate(callbackBaseUri, UriKind.Absolute, out var callbackUri))
    {
        return false;
    }

    if (!string.Equals(callbackUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    httpsPort = callbackUri.Port;
    return httpsPort > 0;
}
