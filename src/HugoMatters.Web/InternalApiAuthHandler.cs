using Microsoft.Extensions.Options;

namespace HugoMatters.Web;

/// <summary>
/// Adds the shared internal API token to outbound ApiService requests.
/// </summary>
public sealed class InternalApiAuthHandler(IOptions<InternalApiOptions> optionsAccessor) : DelegatingHandler
{
    internal const string SharedSecretHeaderName = "X-Hugo-Matters-Internal-Token";

    private readonly InternalApiOptions _options = optionsAccessor.Value;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_options.IsConfigured
            && !request.Headers.Contains(SharedSecretHeaderName))
        {
            request.Headers.TryAddWithoutValidation(SharedSecretHeaderName, _options.SharedSecret);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
