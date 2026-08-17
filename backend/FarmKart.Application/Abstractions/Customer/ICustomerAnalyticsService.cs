using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Customer;

public interface ICustomerAnalyticsService
{
    Task<CustomerAnalyticsOverviewResponse> GetCustomerAnalyticsAsync(
        string customerUserId,
        AnalyticsDateRangeRequest request,
        CancellationToken cancellationToken = default);
}
