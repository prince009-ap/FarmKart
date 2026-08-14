using FarmKart.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Worker;

public interface IWorkerEarningsService
{
    Task<WorkerEarningsSummaryResponse> GetWorkerEarningsAsync(Guid workerUserId);
}
