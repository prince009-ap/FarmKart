using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerAnalyticsService
{
    Task<FarmerAnalyticsOverviewResponse> GetFarmerAnalyticsAsync(
        string farmerUserId,
        AnalyticsDateRangeRequest request,
        CancellationToken cancellationToken = default);
}
