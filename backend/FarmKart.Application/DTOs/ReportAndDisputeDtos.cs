using FarmKart.Domain.Enums;
using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record CreateReportRequest(
    ReportTargetType TargetType,
    Guid TargetId,
    string Reason,
    string Description
);

public record UserReportResponse(
    Guid Id,
    string ReporterUserId,
    ReportTargetType TargetType,
    Guid TargetId,
    string TargetTitle,
    string Reason,
    string Description,
    ReportStatus Status,
    string? ResolutionNote,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record ReportQueryRequest(
    string? Status = null,
    string? TargetType = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
);

public record PagedReportResponse(
    IReadOnlyList<UserReportResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record CreateDisputeRequest(
    DisputeEntityType RelatedEntityType,
    Guid RelatedEntityId,
    string Reason,
    string Description
);

public record DisputeTimelineItemDto(
    string Status,
    string Note,
    DateTime TimestampUtc
);

public record UserDisputeResponse(
    Guid Id,
    string RaisedByUserId,
    DisputeEntityType RelatedEntityType,
    Guid RelatedEntityId,
    string EntityTitle,
    string Reason,
    string Description,
    DisputeStatus Status,
    string? ResolutionNote,
    IReadOnlyList<DisputeTimelineItemDto> Timeline,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record DisputeQueryRequest(
    string? Status = null,
    string? RelatedEntityType = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
);

public record PagedDisputeResponse(
    IReadOnlyList<UserDisputeResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
