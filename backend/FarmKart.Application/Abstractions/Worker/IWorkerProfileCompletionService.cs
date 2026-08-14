using FarmKart.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Worker;

public interface IWorkerProfileCompletionService
{
    Task<WorkerProfileCompletionResponse> GetProfileCompletionAsync(Guid workerUserId);
}
