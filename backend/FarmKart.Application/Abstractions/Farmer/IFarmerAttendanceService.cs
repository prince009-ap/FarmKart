using FarmKart.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerAttendanceService
{
    Task<IReadOnlyList<FarmerAttendanceResponse>> GetJobAttendanceAsync(Guid userId, Guid jobId, DateOnly? date = null);
    Task<IReadOnlyList<FarmerAttendanceResponse>> SaveJobAttendanceAsync(Guid userId, Guid jobId, SaveJobAttendanceRequest request);
    Task<FarmerAttendanceResponse> UpdateAttendanceRecordAsync(Guid userId, Guid attendanceId, UpdateAttendanceRecordRequest request);
}
