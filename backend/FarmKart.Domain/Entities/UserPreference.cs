using FarmKart.Domain.Common;

namespace FarmKart.Domain.Entities;

public sealed class UserPreference : BaseEntity
{
    public Guid UserId { get; set; }
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "en";
    public bool EmailAlerts { get; set; } = true;
    public bool SmsAlerts { get; set; } = false;
    public bool CompactView { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
