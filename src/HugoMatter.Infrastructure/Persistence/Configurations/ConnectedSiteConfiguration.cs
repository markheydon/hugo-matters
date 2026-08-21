using HugoMatter.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HugoMatter.Infrastructure.Persistence.Configurations;

internal sealed class ConnectedSiteConfiguration : IEntityTypeConfiguration<ConnectedSite>
{
    public void Configure(EntityTypeBuilder<ConnectedSite> builder)
    {
        builder.ToTable("sites");
        builder.HasKey(site => site.Id);
        builder.Property(site => site.OwnerLogin).HasMaxLength(256).IsRequired();
        builder.Property(site => site.RepoName).HasMaxLength(256).IsRequired();
        builder.Property(site => site.DefaultBranch).HasMaxLength(256).IsRequired();
        builder.Property(site => site.HtmlUrl).HasMaxLength(2048);
        builder.Property(site => site.ThemePackId).HasMaxLength(128).IsRequired();
        builder.Property(site => site.ThemePackVersion).HasMaxLength(64);
        builder.Property(site => site.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
    }
}
