using Microsoft.AspNetCore.Components.Web;

namespace HugoMatters.Web;

/// <summary>
/// Shared Blazor render modes for interactive pages.
/// </summary>
public static class AppRenderMode
{
    /// <summary>
    /// Interactive Server with prerender so the loading UI is in the first HTML response.
    /// Pages should defer API work to <see cref="Components.DeferredLoadPageBase"/> (or equivalent)
    /// so prerender is not blocked on network calls.
    /// </summary>
    public static InteractiveServerRenderMode Interactive { get; } = new(prerender: true);
}
