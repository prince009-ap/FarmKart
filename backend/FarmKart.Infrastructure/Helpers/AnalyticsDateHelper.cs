using System;
using FarmKart.Application.DTOs;

namespace FarmKart.Infrastructure.Helpers;

public static class AnalyticsDateHelper
{
    public static (DateTime FromDateUtc, DateTime ToDateUtc, string Label) CalculateDateRange(AnalyticsDateRangeRequest request)
    {
        var now = DateTime.UtcNow;

        switch (request.Range)
        {
            case AnalyticsDateRange.Today:
                var startOfToday = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
                return (startOfToday, now, "Today");

            case AnalyticsDateRange.Last7Days:
                return (now.AddDays(-7), now, "Last 7 Days");

            case AnalyticsDateRange.Last30Days:
                return (now.AddDays(-30), now, "Last 30 Days");

            case AnalyticsDateRange.ThisMonth:
                var startOfThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                return (startOfThisMonth, now, "This Month");

            case AnalyticsDateRange.LastMonth:
                var lastMonthDate = now.AddMonths(-1);
                var startOfLastMonth = new DateTime(lastMonthDate.Year, lastMonthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var daysInLastMonth = DateTime.DaysInMonth(lastMonthDate.Year, lastMonthDate.Month);
                var endOfLastMonth = new DateTime(lastMonthDate.Year, lastMonthDate.Month, daysInLastMonth, 23, 59, 59, DateTimeKind.Utc);
                return (startOfLastMonth, endOfLastMonth, "Last Month");

            case AnalyticsDateRange.ThisYear:
                var startOfThisYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return (startOfThisYear, now, "This Year");

            case AnalyticsDateRange.Custom:
                var from = request.CustomStartDateUtc ?? now.AddDays(-30);
                var to = request.CustomEndDateUtc ?? now;
                if (from > to)
                {
                    (from, to) = (to, from);
                }
                return (from, to, $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}");

            default:
                return (now.AddDays(-30), now, "Last 30 Days");
        }
    }
}
