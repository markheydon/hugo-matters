using System.Collections.Concurrent;
using HugoMatters.Core.Connection;
using HugoMatters.Core.Content;
using HugoMatters.Core.Ports;
using HugoMatters.Core.Preview;
using HugoMatters.Core.Sessions;
using HugoMatters.Infrastructure.GitHub;
using HugoMatters.Infrastructure.Persistence;
using HugoMatters.Infrastructure.Preview;
using HugoMatters.ThemePacks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HugoMatters.Infrastructure;

/// <summary>
/// Infrastructure service registration for Hugo Matters.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure implementations and Core application services.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<HugoMattersDataOptions>(configuration.GetSection(HugoMattersDataOptions.SectionName));
        services.Configure<GitHubAppOptions>(configuration.GetSection(GitHubAppOptions.SectionName));
        services.Configure<DockerSitePreviewOptions>(configuration.GetSection(DockerSitePreviewOptions.SectionName));

        var dataDirectory = configuration
            .GetSection(HugoMattersDataOptions.SectionName)
            .Get<HugoMattersDataOptions>()?
            .ResolveDataDirectory()
            ?? new HugoMattersDataOptions().ResolveDataDirectory();
        Directory.CreateDirectory(dataDirectory);

        var databasePath = Path.Combine(dataDirectory, "hugo-matters.db");
        services.AddDbContext<HugoMattersDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        services.AddHttpClient(nameof(GitHubConnectionHandler));
        services.AddHttpClient(nameof(DockerSitePreviewOrchestrator));

        services.AddSingleton<IThemePackRegistry, ThemePackRegistry>();
        services.AddSingleton<GitHubAppJwtFactory>();
        services.AddSingleton<IGitHubRepository, GitHubAppClient>();
        services.AddScoped<GitHubConnectionHandler>();
        services.AddScoped<SessionRepository>();
        services.AddScoped<IMetadataStore, EfMetadataStore>();
        services.AddSingleton<IContentBufferStore, InMemoryContentBufferStore>();
        services.AddScoped<ISitePreviewOrchestrator, DockerSitePreviewOrchestrator>();

        services.AddScoped<ConnectionService>();
        services.AddScoped<SessionService>();
        services.AddScoped<SaveService>();
        services.AddScoped<PublishService>();
        services.AddScoped<DiscardService>();
        services.AddScoped<ContentBufferService>();
        services.AddScoped<SiteConfigService>();
        services.AddSingleton<EditorPreviewService>();

        return services;
    }

    /// <summary>
    /// Ensures the metadata database schema exists.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DatabaseInitializationLocks = new();

    public static async Task InitializeInfrastructureAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HugoMattersDbContext>();
        var connectionString = dbContext.Database.GetDbConnection().ConnectionString ?? string.Empty;
        var gate = DatabaseInitializationLocks.GetOrAdd(connectionString, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
        finally
        {
            gate.Release();
        }
    }
}
