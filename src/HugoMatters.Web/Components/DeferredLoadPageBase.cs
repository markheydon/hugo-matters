using Microsoft.AspNetCore.Components;

namespace HugoMatters.Web.Components;

/// <summary>
/// Defers page data loading until the interactive circuit is ready, while prerender
/// paints the initial loading UI immediately (avoids blank flashes and blocked first paint).
/// </summary>
public abstract class DeferredLoadPageBase : ComponentBase
{
    private bool _loadStarted;

    /// <summary>
    /// Loads page data. Called once on the interactive render.
    /// </summary>
    protected abstract Task LoadPageAsync();

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_loadStarted || !RendererInfo.IsInteractive)
        {
            return;
        }

        _loadStarted = true;
        await LoadPageAsync().ConfigureAwait(false);
        await InvokeAsync(StateHasChanged).ConfigureAwait(false);
    }
}
