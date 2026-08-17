using FarmKart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class UserReportConfiguration : IEntityTypeConfiguration<UserReport>
{
    public void Configure(EntityTypeBuilder<UserReport> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReporterUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.Reason)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(r => r.ResolutionNote)
            .HasMaxLength(2000);

        builder.HasIndex(r => r.ReporterUserId);
        builder.HasIndex(r => new { r.ReporterUserId, r.TargetType, r.TargetId });
    }
}

public sealed class UserDisputeConfiguration : IEntityTypeConfiguration<UserDispute>
{
    public void Configure(EntityTypeBuilder<UserDispute> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.RaisedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(d => d.Reason)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(d => d.ResolutionNote)
            .HasMaxLength(2000);

        builder.HasIndex(d => d.RaisedByUserId);
        builder.HasIndex(d => new { d.RaisedByUserId, d.RelatedEntityType, d.RelatedEntityId });
    }
}
