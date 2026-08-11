using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;

namespace FarmKart.Domain.Entities;

public sealed class Job : BaseEntity
{
    public Guid FarmerProfileId { get; set; }
    public FarmerProfile FarmerProfile { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string WorkCategory { get; set; } = string.Empty;
    public string? CropType { get; set; }
    public int WorkersRequired { get; set; }
    public int RequiredExperience { get; set; }
    public decimal WagePerDay { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string WorkingHours { get; set; } = string.Empty;
    public string FarmLocation { get; set; } = string.Empty;
    public decimal? FarmSize { get; set; }
    public bool FoodProvided { get; set; }
    public bool AccommodationProvided { get; set; }
    public bool IsUrgent { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Draft;

    public ICollection<JobApplication> JobApplications { get; set; } = [];
    public ICollection<WorkerAssignment> WorkerAssignments { get; set; } = [];
}

public sealed class JobApplication : BaseEntity
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public Guid WorkerProfileId { get; set; }
    public WorkerProfile WorkerProfile { get; set; } = null!;
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public string? Message { get; set; }
}

public sealed class WorkerAssignment : BaseEntity
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public Guid WorkerProfileId { get; set; }
    public WorkerProfile WorkerProfile { get; set; } = null!;
    public Guid? JobApplicationId { get; set; }
    public JobApplication? JobApplication { get; set; }
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;

    public ICollection<Attendance> Attendances { get; set; } = [];
    public ICollection<WorkerPayment> WorkerPayments { get; set; } = [];
}

public sealed class Attendance : BaseEntity
{
    public Guid WorkerAssignmentId { get; set; }
    public WorkerAssignment WorkerAssignment { get; set; } = null!;
    public DateOnly Date { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public decimal TotalHours { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Notes { get; set; }
}

public sealed class WorkerPayment : BaseEntity
{
    public Guid WorkerAssignmentId { get; set; }
    public WorkerAssignment WorkerAssignment { get; set; } = null!;
    public Guid WorkerProfileId { get; set; }
    public WorkerProfile WorkerProfile { get; set; } = null!;
    public Guid FarmerProfileId { get; set; }
    public FarmerProfile FarmerProfile { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Other;
    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }
}
