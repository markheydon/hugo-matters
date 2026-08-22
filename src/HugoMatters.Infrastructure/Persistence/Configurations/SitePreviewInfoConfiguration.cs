using HugoMatters.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HugoMatters.Infrastructure.Persistence.Configurations;

internal sealed class SitePreviewInfoConfiguration : IEntityTypeConfiguration<SitePreviewInfo>
{
    public void Configure(EntityTypeBuilder<SitePreviewInfo> builder)
    {
        builder.ToTable("previews");
        builder.HasKey(preview => preview.Id);
        builder.Property(preview => preview.Id).HasMaxLength(128);
        builder.Property(preview => preview.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(preview => preview.BaseUrl).HasMaxLength(2048);
        builder.Property(preview => preview.WorkspacePath).HasMaxLength(4096);
        builder.Property(preview => preview.SourceRef).HasMaxLength(64);
        builder.Property(preview => preview.ErrorMessage).HasMaxLength(4096);
        builder.HasIndex(preview => preview.SessionId).IsUnique();
    }
}
