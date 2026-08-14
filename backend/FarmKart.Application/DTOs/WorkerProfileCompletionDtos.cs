using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record ProfileCompletionSectionResponse(
    string SectionKey,
    string SectionName,
    bool IsComplete,
    int CompletionPercentage,
    string Description,
    string ActionRoute
);

public record WorkerProfileCompletionResponse(
    int OverallCompletionPercentage,
    string VerificationStatus,
    IReadOnlyList<ProfileCompletionSectionResponse> Sections
);
