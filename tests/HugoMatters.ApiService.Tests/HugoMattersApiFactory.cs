using System.Text.Json;
using System.Text.Json.Serialization;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HugoMatters:MetadataConnectionString"] = "InMemory",
                ["GitHubApp:ClientId"] = "test-client-id",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGitHubRepository>();
            services.RemoveAll<IMetadataStore>();
            services.AddSingleton(GitHub);
            services.AddSingleton<IMetadataStore, InMemoryMetadataStore>();
        });
    }
}
