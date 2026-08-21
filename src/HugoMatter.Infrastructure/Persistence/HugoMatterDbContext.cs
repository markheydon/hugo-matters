using HugoMatter.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HugoMatter.Infrastructure.Persistence;

/// <summary>
/// SQLite metadata persistence for connected sites, sessions, and previews.
/// </summary>
public sealed class HugoMatterDbContext : DbContext
{
    /// <summary>
    /// Creates a new <see cref="HugoMatterDbContext"/>.
    /// </summary>
    public HugoMatterDbContext(DbContextOptions<HugoMatterDbContext> options)
        : base(options)
    {
    }

    /// <summary>Connected site rows.</summary>
    public DbSet<ConnectedSite> Sites => Set<ConnectedSite>();

    /// <summary>Editing session rows.</summary>
    public DbSet<EditingSession> Sessions => Set<EditingSession>();

    /// <summary>Site preview metadata rows.</summary>
    public DbSet<SitePreviewInfo> Previews => Set<SitePreviewInfo>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HugoMatterDbContext).Assembly);
    }
}
