using FarmKart.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Worker;

public interface IWorkerWorkHistoryService
{
    Task<WorkerWorkHistorySummaryResponse> GetWorkerWorkHistoryAsync(Guid workerUserId);
}
