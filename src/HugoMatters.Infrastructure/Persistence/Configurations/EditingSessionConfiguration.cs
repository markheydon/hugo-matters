using HugoMatters.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HugoMatters.Infrastructure.Persistence.Configurations;

internal sealed class EditingSessionConfiguration : IEntityTypeConfiguration<EditingSession>
{
    public void Configure(EntityTypeBuilder<EditingSession> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.BranchName).HasMaxLength(256).IsRequired();
        builder.Property(session => session.PullRequestUrl).HasMaxLength(2048);
        builder.Property(session => session.BaseBranch).HasMaxLength(256).IsRequired();
        builder.Property(session => session.State).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(session => session.SiteId);
        builder.HasIndex(session => new { session.SiteId, session.State })
            .IsUnique()
            .HasFilter($"\"State\" = '{SessionState.Active}'");
    }
}
