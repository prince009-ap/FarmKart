using FarmKart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(skill => skill.Name).HasMaxLength(120).IsRequired();
        builder.Property(skill => skill.Description).HasMaxLength(500);

        builder.HasIndex(skill => skill.Name).IsUnique();
    }
}

public sealed class WorkerSkillConfiguration : IEntityTypeConfiguration<WorkerSkill>
{
    public void Configure(EntityTypeBuilder<WorkerSkill> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_WorkerSkill_ProficiencyScore_Range", "[ProficiencyScore] IS NULL OR ([ProficiencyScore] >= 0 AND [ProficiencyScore] <= 100)");
            table.HasCheckConstraint("CK_WorkerSkill_ExperienceYears_NonNegative", "[ExperienceYears] IS NULL OR [ExperienceYears] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(workerSkill => workerSkill.ProficiencyScore).HasPrecision(5, 2);
        builder.HasIndex(workerSkill => new { workerSkill.WorkerProfileId, workerSkill.SkillId }).IsUnique();

        builder.HasOne(workerSkill => workerSkill.WorkerProfile)
            .WithMany(worker => worker.WorkerSkills)
            .HasForeignKey(workerSkill => workerSkill.WorkerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(workerSkill => workerSkill.Skill)
            .WithMany(skill => skill.WorkerSkills)
            .HasForeignKey(workerSkill => workerSkill.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
