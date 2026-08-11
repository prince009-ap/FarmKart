using FarmKart.Domain.Common;

namespace FarmKart.Domain.Entities;

public sealed class Skill : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<WorkerSkill> WorkerSkills { get; set; } = [];
}

public sealed class WorkerSkill : BaseEntity
{
    public Guid WorkerProfileId { get; set; }
    public WorkerProfile WorkerProfile { get; set; } = null!;
    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
    public decimal? ProficiencyScore { get; set; }
    public int? ExperienceYears { get; set; }
}
