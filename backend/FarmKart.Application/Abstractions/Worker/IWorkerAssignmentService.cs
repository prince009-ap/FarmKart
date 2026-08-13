using FarmKart.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Worker;

public interface IWorkerAssignmentService
{
    Task<IReadOnlyList<WorkerAssignmentResponse>> GetMyAssignmentsAsync(Guid userId);
    Task<WorkerAssignmentResponse> GetAssignmentDetailsAsync(Guid userId, Guid assignmentId);
}
