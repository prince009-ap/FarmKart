using FarmKart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Job_WorkersRequired_Positive", "[WorkersRequired] > 0");
            table.HasCheckConstraint("CK_Job_RequiredExperience_NonNegative", "[RequiredExperience] >= 0");
            table.HasCheckConstraint("CK_Job_WagePerDay_NonNegative", "[WagePerDay] >= 0");
            table.HasCheckConstraint("CK_Job_FarmSize_NonNegative", "[FarmSize] IS NULL OR [FarmSize] >= 0");
            table.HasCheckConstraint("CK_Job_EndDate_After_StartDate", "[EndDate] >= [StartDate]");
        });

        builder.ConfigureBaseEntity();

        builder.Property(job => job.Title).HasMaxLength(150).IsRequired();
        builder.Property(job => job.Description).HasMaxLength(2000).IsRequired();
        builder.Property(job => job.WorkCategory).HasMaxLength(100).IsRequired();
        builder.Property(job => job.CropType).HasMaxLength(100);
        builder.Property(job => job.WagePerDay).HasPrecision(18, 2);
        builder.Property(job => job.WorkingHours).HasMaxLength(100).IsRequired();
        builder.Property(job => job.FarmLocation).HasMaxLength(250).IsRequired();
        builder.Property(job => job.FarmSize).HasPrecision(18, 2);

        builder.HasIndex(job => job.FarmerProfileId);
        builder.HasIndex(job => job.Status);

        builder.HasOne(job => job.FarmerProfile)
            .WithMany(farmer => farmer.Jobs)
            .HasForeignKey(job => job.FarmerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(application => application.Message).HasMaxLength(1000);
        builder.HasIndex(application => new { application.JobId, application.WorkerProfileId }).IsUnique();

        builder.HasOne(application => application.Job)
            .WithMany(job => job.JobApplications)
            .HasForeignKey(application => application.JobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(application => application.WorkerProfile)
            .WithMany(worker => worker.JobApplications)
            .HasForeignKey(application => application.WorkerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkerAssignmentConfiguration : IEntityTypeConfiguration<WorkerAssignment>
{
    public void Configure(EntityTypeBuilder<WorkerAssignment> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_WorkerAssignment_EndDate_After_StartDate", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
        });

        builder.ConfigureBaseEntity();

        builder.HasIndex(assignment => assignment.JobId);
        builder.HasIndex(assignment => assignment.WorkerProfileId);

        builder.HasOne(assignment => assignment.Job)
            .WithMany(job => job.WorkerAssignments)
            .HasForeignKey(assignment => assignment.JobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(assignment => assignment.WorkerProfile)
            .WithMany(worker => worker.WorkerAssignments)
            .HasForeignKey(assignment => assignment.WorkerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(assignment => assignment.JobApplication)
            .WithMany()
            .HasForeignKey(assignment => assignment.JobApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Attendance_TotalHours_Range", "[TotalHours] >= 0 AND [TotalHours] <= 24");
        });

        builder.ConfigureBaseEntity();

        builder.Property(attendance => attendance.TotalHours).HasPrecision(5, 2);
        builder.Property(attendance => attendance.Notes).HasMaxLength(500);
        builder.HasIndex(attendance => new { attendance.WorkerAssignmentId, attendance.Date }).IsUnique();

        builder.HasOne(attendance => attendance.WorkerAssignment)
            .WithMany(assignment => assignment.Attendances)
            .HasForeignKey(attendance => attendance.WorkerAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkerPaymentConfiguration : IEntityTypeConfiguration<WorkerPayment>
{
    public void Configure(EntityTypeBuilder<WorkerPayment> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_WorkerPayment_Amount_NonNegative", "[Amount] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.TransactionReference).HasMaxLength(150);
        builder.Property(payment => payment.Notes).HasMaxLength(500);

        builder.HasIndex(payment => payment.WorkerProfileId);
        builder.HasIndex(payment => payment.FarmerProfileId);

        builder.HasOne(payment => payment.WorkerAssignment)
            .WithMany(assignment => assignment.WorkerPayments)
            .HasForeignKey(payment => payment.WorkerAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payment => payment.WorkerProfile)
            .WithMany(worker => worker.WorkerPayments)
            .HasForeignKey(payment => payment.WorkerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payment => payment.FarmerProfile)
            .WithMany(farmer => farmer.WorkerPayments)
            .HasForeignKey(payment => payment.FarmerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
