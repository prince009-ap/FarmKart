using FarmKart.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Report;

public interface IReportService
{
    Task<UserReportResponse> CreateReportAsync(Guid userId, CreateReportRequest request, CancellationToken cancellationToken = default);
    Task<PagedReportResponse> GetUserReportsAsync(Guid userId, ReportQueryRequest request, CancellationToken cancellationToken = default);
    Task<UserReportResponse?> GetReportByIdAsync(Guid userId, Guid reportId, CancellationToken cancellationToken = default);
}
