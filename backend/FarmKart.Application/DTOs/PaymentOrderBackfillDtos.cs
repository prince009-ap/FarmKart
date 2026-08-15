namespace FarmKart.Application.DTOs;

public sealed record PaymentOrderBackfillItemResult(
    Guid PaymentId,
    Guid AuctionId,
    Guid CustomerProfileId,
    Guid? AllocationId,
    decimal PaymentAmount,
    decimal AllocatedQuantityKg,
    decimal PricePerMan,
    decimal ExpectedAmount,
    string ResultStatus,
    string Reason,
    string? OrderNumber = null
);

public sealed record PaymentOrderBackfillResult(
    bool DryRun,
    int TotalPaidPaymentsFound,
    int AlreadyHaveOrders,
    int MissingOrders,
    int ValidForBackfill,
    int OrdersCreated,
    int TotalSkipped,
    IReadOnlyList<PaymentOrderBackfillItemResult> ItemResults
);
