namespace FarmKart.Application.Abstractions.Persistence;

public interface IFarmKartDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
