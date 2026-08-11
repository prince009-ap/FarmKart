using FarmKart.Domain.ValueObjects;

namespace FarmKart.Domain.Common;

public abstract class ProfileBase : BaseEntity
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public AddressInfo AddressInfo { get; set; } = new();
}
