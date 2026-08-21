using HugoMatter.Core.Connection;
using HugoMatter.Core.Content;
using HugoMatter.Core.Ports;
using HugoMatter.Core.Preview;
using HugoMatter.Core.Sessions;
using HugoMatter.Infrastructure.GitHub;
using HugoMatter.Infrastructure.Persistence;
using HugoMatter.Infrastructure.Preview;
using HugoMatter.ThemePacks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HugoMatter.Infrastructure;

/// <summary>
/// Infrastructure service registration for Hugo Matter.
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

        services.Configure<HugoMatterDataOptions>(configuration.GetSection(HugoMatterDataOptions.SectionName));
        services.Configure<GitHubAppOptions>(configuration.GetSection(GitHubAppOptions.SectionName));
        services.Configure<DockerSitePreviewOptions>(configuration.GetSection(DockerSitePreviewOptions.SectionName));

        var dataDirectory = configuration
            .GetSection(HugoMatterDataOptions.SectionName)
            .Get<HugoMatterDataOptions>()?
            .ResolveDataDirectory()
            ?? new HugoMatterDataOptions().ResolveDataDirectory();
        Directory.CreateDirectory(dataDirectory);

        var databasePath = Path.Combine(dataDirectory, "hugo-matter.db");
        services.AddDbContext<HugoMatterDbContext>(options =>
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

        services.AddHostedService<InfrastructureDatabaseInitializer>();

        return services;
    }

    /// <summary>
    /// Ensures the metadata database schema exists.
    /// </summary>
    public static async Task InitializeInfrastructureAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HugoMatterDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    private sealed class InfrastructureDatabaseInitializer : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public InfrastructureDatabaseInitializer(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<HugoMatterDbContext>();
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
