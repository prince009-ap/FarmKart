using FarmKart.Application.DTOs;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Authentication;

public interface IAuthService
{
    Task<FarmerRegistrationResponse> RegisterFarmerAsync(FarmerRegisterRequest request);
    Task<WorkerRegistrationResponse> RegisterWorkerAsync(WorkerRegisterRequest request);
    Task<CustomerRegistrationResponse> RegisterCustomerAsync(CustomerRegisterRequest request);
    Task<LoginResult> LoginAsync(LoginRequest request);
    Task<AuthUserResponse> GetCurrentUserAsync(System.Guid userId, string role);
}
