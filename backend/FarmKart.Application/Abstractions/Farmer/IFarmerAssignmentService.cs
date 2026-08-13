using FarmKart.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerAssignmentService
{
    Task<IReadOnlyList<FarmerWorkerAssignmentResponse>> GetAssignmentsForJobAsync(Guid userId, Guid jobId);
}
