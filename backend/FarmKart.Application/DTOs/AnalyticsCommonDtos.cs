using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public enum AnalyticsDateRange
{
    Today,
    Last7Days,
    Last30Days,
    ThisMonth,
    LastMonth,
    ThisYear,
    Custom
}

public record AnalyticsDateRangeRequest(
    AnalyticsDateRange Range = AnalyticsDateRange.Last30Days,
    DateTime? CustomStartDateUtc = null,
    DateTime? CustomEndDateUtc = null
);

public record TimeSeriesPointDto(
    string Label,
    DateTime DateUtc,
    decimal Value
);

public record TimeSeriesChartDto(
    string MetricName,
    string TimeGroup, // "Daily", "Weekly", "Monthly"
    IReadOnlyList<TimeSeriesPointDto> Points
);

public record RatingDistributionDto(
    int FiveStar,
    int FourStar,
    int ThreeStar,
    int TwoStar,
    int OneStar
);
