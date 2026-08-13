using FarmKart.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerApplicationService
{
    Task<IReadOnlyList<FarmerJobApplicationResponse>> GetApplicationsForJobAsync(Guid userId, Guid jobId);
    Task<FarmerJobApplicationResponse> GetApplicationDetailsAsync(Guid userId, Guid applicationId);
    Task<FarmerJobApplicationResponse> AcceptApplicationAsync(Guid userId, Guid applicationId);
    Task<FarmerJobApplicationResponse> RejectApplicationAsync(Guid userId, Guid applicationId);
}
