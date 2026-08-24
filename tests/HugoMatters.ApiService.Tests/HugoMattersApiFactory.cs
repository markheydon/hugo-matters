using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HugoMatters.ApiService.Security;
using HugoMatters.Core.Json;
using HugoMatters.Core.Ports;
using HugoMatters.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace HugoMatters.ApiService.Tests;

/// <summary>
/// Web application factory for API integration tests.
/// </summary>
public sealed class HugoMattersApiFactory : WebApplicationFactory<Program>
{
    internal const string TestInternalApiToken = "test-internal-token";

    /// <summary>When false, GitHub App JWT credentials are omitted from test configuration.</summary>
    public bool IncludeGitHubAppCredentials { get; init; } = true;

    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "hugo-matters-tests",
        Guid.NewGuid().ToString("N"));

    /// <summary>Substituted GitHub repository port.</summary>
    public IGitHubRepository GitHub { get; } = Substitute.For<IGitHubRepository>();

    /// <summary>JSON options matching API serialization.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(),
            new ContentTypeKindJsonConverter(),
        },
    };

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            InternalApiAuthenticationMiddleware.SharedSecretHeaderName,
            TestInternalApiToken);
        return client;
    }

    public HttpClient CreateClientWithoutInternalToken() => base.CreateClient();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("HugoMatters:DataDirectory", _dataDirectory);

        builder.ConfigureAppConfiguration(config =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["HugoMatters:DataDirectory"] = _dataDirectory,
                ["InternalApi:SharedSecret"] = TestInternalApiToken,
            };

            if (IncludeGitHubAppCredentials)
            {
                settings["GitHubApp:AppId"] = "1";
                settings["GitHubApp:PrivateKeyPem"] = "test-private-key";
            }

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGitHubRepository>();
            services.RemoveAll<IMetadataStore>();
            services.AddSingleton(GitHub);
            services.AddSingleton<IMetadataStore, InMemoryMetadataStore>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(_dataDirectory))
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
