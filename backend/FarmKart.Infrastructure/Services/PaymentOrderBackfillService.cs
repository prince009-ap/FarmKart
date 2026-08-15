using System.Data;
using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.Common;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FarmKart.Infrastructure.Services;

public sealed class PaymentOrderBackfillService(
    FarmKartDbContext dbContext,
    ILogger<PaymentOrderBackfillService> logger) : IPaymentOrderBackfillService
{
    public async Task<PaymentOrderBackfillResult> ExecuteBackfillAsync(
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Payment Order Backfill (DryRun = {DryRun})...", dryRun);

        // Fetch all PAID payments with relationships
        var paidPayments = await dbContext.AuctionPayments
            .AsNoTracking()
            .Include(p => p.Auction)
                .ThenInclude(a => a.CropListing)
                    .ThenInclude(l => l.Crop)
            .Include(p => p.Auction)
                .ThenInclude(a => a.FarmerProfile)
            .Include(p => p.Auction)
                .ThenInclude(a => a.Allocations)
            .Include(p => p.AuctionOrder)
            .Where(p => p.PaymentStatus == PaymentStatus.Paid)
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var totalFound = paidPayments.Count;
        var alreadyHaveOrders = 0;
        var validForBackfill = 0;
        var ordersCreated = 0;
        var totalSkipped = 0;

        var itemResults = new List<PaymentOrderBackfillItemResult>();

        foreach (var payment in paidPayments)
        {
            // 1. Check if order already exists
            if (payment.AuctionOrder is not null ||
                await dbContext.AuctionOrders.AnyAsync(o => o.AuctionPaymentId == payment.Id, cancellationToken))
            {
                alreadyHaveOrders++;
                totalSkipped++;
                var item = new PaymentOrderBackfillItemResult(
                    PaymentId: payment.Id,
                    AuctionId: payment.AuctionId,
                    CustomerProfileId: payment.CustomerProfileId,
                    AllocationId: payment.AuctionOrder?.AuctionAllocationId,
                    PaymentAmount: payment.Amount,
                    AllocatedQuantityKg: payment.AuctionOrder?.AllocatedQuantityKg ?? 0m,
                    PricePerMan: payment.AuctionOrder?.PricePerMan ?? 0m,
                    ExpectedAmount: payment.Amount,
                    ResultStatus: "SKIPPED_ALREADY_EXISTS",
                    Reason: "Order already exists for this payment.",
                    OrderNumber: payment.AuctionOrder?.OrderNumber
                );
                itemResults.Add(item);
                logger.LogInformation("Payment {PaymentId}: SKIPPED_ALREADY_EXISTS", payment.Id);
                continue;
            }

            // 2. Validate relationships
            if (payment.Auction is null ||
                payment.Auction.CropListing is null ||
                payment.Auction.CropListing.Crop is null ||
                payment.Auction.FarmerProfile is null)
            {
                totalSkipped++;
                var item = new PaymentOrderBackfillItemResult(
                    PaymentId: payment.Id,
                    AuctionId: payment.AuctionId,
                    CustomerProfileId: payment.CustomerProfileId,
                    AllocationId: null,
                    PaymentAmount: payment.Amount,
                    AllocatedQuantityKg: 0m,
                    PricePerMan: 0m,
                    ExpectedAmount: 0m,
                    ResultStatus: "SKIPPED_MISSING_RELATIONSHIP",
                    Reason: "Missing required auction, crop, or farmer relationship."
                );
                itemResults.Add(item);
                logger.LogWarning("Payment {PaymentId}: SKIPPED_MISSING_RELATIONSHIP", payment.Id);
                continue;
            }

            // 3. Find matching allocation by payment amount calculation or winning status
            var allocation = payment.Auction.Allocations
                .FirstOrDefault(al =>
                    al.CustomerProfileId == payment.CustomerProfileId &&
                    (Math.Abs(payment.Amount - Math.Round(AuctionPricingConstants.ConvertKgToMan(al.AllocatedQuantityKg) * al.WinningBidAmountPerMan, 2)) <= 0.01m ||
                     (al.RequestedQuantityKg > 0 && Math.Abs(payment.Amount - Math.Round(AuctionPricingConstants.ConvertKgToMan(al.RequestedQuantityKg) * al.WinningBidAmountPerMan, 2)) <= 0.01m)))
                ?? payment.Auction.Allocations
                .FirstOrDefault(al =>
                    al.CustomerProfileId == payment.CustomerProfileId &&
                    al.AllocatedQuantityKg > 0 &&
                    (al.Status == AllocationStatus.Won || al.Status == AllocationStatus.PartiallyWon));

            if (allocation is null)
            {
                totalSkipped++;
                var item = new PaymentOrderBackfillItemResult(
                    PaymentId: payment.Id,
                    AuctionId: payment.AuctionId,
                    CustomerProfileId: payment.CustomerProfileId,
                    AllocationId: null,
                    PaymentAmount: payment.Amount,
                    AllocatedQuantityKg: 0m,
                    PricePerMan: 0m,
                    ExpectedAmount: 0m,
                    ResultStatus: "SKIPPED_INVALID_ALLOCATION",
                    Reason: "No winning or matching allocation found for this payment customer."
                );
                itemResults.Add(item);
                logger.LogWarning("Payment {PaymentId}: SKIPPED_INVALID_ALLOCATION", payment.Id);
                continue;
            }

            // Determine effective allocated quantity
            var effectiveQty = allocation.AllocatedQuantityKg > 0 ? allocation.AllocatedQuantityKg : allocation.RequestedQuantityKg;
            if (effectiveQty <= 0)
            {
                totalSkipped++;
                var item = new PaymentOrderBackfillItemResult(
                    PaymentId: payment.Id,
                    AuctionId: payment.AuctionId,
                    CustomerProfileId: payment.CustomerProfileId,
                    AllocationId: allocation.Id,
                    PaymentAmount: payment.Amount,
                    AllocatedQuantityKg: 0m,
                    PricePerMan: allocation.WinningBidAmountPerMan,
                    ExpectedAmount: 0m,
                    ResultStatus: "SKIPPED_INVALID_ALLOCATION",
                    Reason: "Allocation quantity is zero."
                );
                itemResults.Add(item);
                continue;
            }

            // 4. Calculate and validate expected payment amount
            var expectedAmount = Math.Round(
                AuctionPricingConstants.ConvertKgToMan(effectiveQty) * allocation.WinningBidAmountPerMan, 2);

            if (Math.Abs(payment.Amount - expectedAmount) > 0.01m)
            {
                totalSkipped++;
                var item = new PaymentOrderBackfillItemResult(
                    PaymentId: payment.Id,
                    AuctionId: payment.AuctionId,
                    CustomerProfileId: payment.CustomerProfileId,
                    AllocationId: allocation.Id,
                    PaymentAmount: payment.Amount,
                    AllocatedQuantityKg: effectiveQty,
                    PricePerMan: allocation.WinningBidAmountPerMan,
                    ExpectedAmount: expectedAmount,
                    ResultStatus: "SKIPPED_AMOUNT_MISMATCH",
                    Reason: $"Payment amount ₹{payment.Amount} does not match expected ₹{expectedAmount} ({effectiveQty} Kg @ ₹{allocation.WinningBidAmountPerMan}/Man)."
                );
                itemResults.Add(item);
                logger.LogWarning("Payment {PaymentId}: SKIPPED_AMOUNT_MISMATCH (Payment: ₹{Amount}, Expected: ₹{Expected})", payment.Id, payment.Amount, expectedAmount);
                continue;
            }

            validForBackfill++;

            // 5. Execution vs Dry Run
            if (dryRun)
            {
                var item = new PaymentOrderBackfillItemResult(
                    PaymentId: payment.Id,
                    AuctionId: payment.AuctionId,
                    CustomerProfileId: payment.CustomerProfileId,
                    AllocationId: allocation.Id,
                    PaymentAmount: payment.Amount,
                    AllocatedQuantityKg: allocation.AllocatedQuantityKg,
                    PricePerMan: allocation.WinningBidAmountPerMan,
                    ExpectedAmount: expectedAmount,
                    ResultStatus: "DRY_RUN_ELIGIBLE",
                    Reason: "Eligible for order creation."
                );
                itemResults.Add(item);
                logger.LogInformation("Payment {PaymentId}: DRY_RUN_ELIGIBLE", payment.Id);
            }
            else
            {
                try
                {
                    var createdOrder = await CreateOrderForPaymentAsync(payment, allocation, expectedAmount, cancellationToken);
                    ordersCreated++;

                    var item = new PaymentOrderBackfillItemResult(
                        PaymentId: payment.Id,
                        AuctionId: payment.AuctionId,
                        CustomerProfileId: payment.CustomerProfileId,
                        AllocationId: allocation.Id,
                        PaymentAmount: payment.Amount,
                        AllocatedQuantityKg: allocation.AllocatedQuantityKg,
                        PricePerMan: allocation.WinningBidAmountPerMan,
                        ExpectedAmount: expectedAmount,
                        ResultStatus: "CREATED",
                        Reason: "Order created successfully.",
                        OrderNumber: createdOrder.OrderNumber
                    );
                    itemResults.Add(item);
                    logger.LogInformation("Payment {PaymentId}: CREATED Order {OrderNumber}", payment.Id, createdOrder.OrderNumber);
                }
                catch (Exception ex)
                {
                    totalSkipped++;
                    var item = new PaymentOrderBackfillItemResult(
                        PaymentId: payment.Id,
                        AuctionId: payment.AuctionId,
                        CustomerProfileId: payment.CustomerProfileId,
                        AllocationId: allocation.Id,
                        PaymentAmount: payment.Amount,
                        AllocatedQuantityKg: allocation.AllocatedQuantityKg,
                        PricePerMan: allocation.WinningBidAmountPerMan,
                        ExpectedAmount: expectedAmount,
                        ResultStatus: "CREATION_FAILED",
                        Reason: $"Order creation failed: {ex.Message}"
                    );
                    itemResults.Add(item);
                    logger.LogError(ex, "Payment {PaymentId}: CREATION_FAILED", payment.Id);
                }
            }
        }

        var missingOrders = totalFound - alreadyHaveOrders;

        var result = new PaymentOrderBackfillResult(
            DryRun: dryRun,
            TotalPaidPaymentsFound: totalFound,
            AlreadyHaveOrders: alreadyHaveOrders,
            MissingOrders: missingOrders,
            ValidForBackfill: validForBackfill,
            OrdersCreated: ordersCreated,
            TotalSkipped: totalSkipped,
            ItemResults: itemResults
        );

        logger.LogInformation("Completed Payment Order Backfill (DryRun = {DryRun}): Total: {Total}, AlreadyExisting: {Existing}, Created: {Created}, Skipped: {Skipped}",
            dryRun, totalFound, alreadyHaveOrders, ordersCreated, totalSkipped);

        return result;
    }

    private async Task<AuctionOrder> CreateOrderForPaymentAsync(
        AuctionPayment payment,
        AuctionAllocation allocation,
        decimal totalAmount,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            // Re-verify idempotency inside transaction
            var existingOrder = await dbContext.AuctionOrders
                .FirstOrDefaultAsync(o => o.AuctionPaymentId == payment.Id, cancellationToken);

            if (existingOrder is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return existingOrder;
            }

            // Generate OrderNumber: FK-YYYYMMDD-NNNN
            var today = DateTime.UtcNow;
            var dateStr = today.ToString("yyyyMMdd");
            var todayStart = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0, DateTimeKind.Utc);
            var todayEnd = todayStart.AddDays(1);

            var countToday = await dbContext.AuctionOrders
                .CountAsync(o => o.CreatedAtUtc >= todayStart && o.CreatedAtUtc < todayEnd, cancellationToken);
            var seqNumber = (countToday + 1).ToString("D4");
            var orderNumber = $"FK-{dateStr}-{seqNumber}";

            var exists = await dbContext.AuctionOrders.AnyAsync(o => o.OrderNumber == orderNumber, cancellationToken);
            if (exists)
            {
                orderNumber = $"FK-{dateStr}-{today:HHmmss}-{seqNumber}";
            }

            var effectiveQty = allocation.AllocatedQuantityKg > 0 ? allocation.AllocatedQuantityKg : allocation.RequestedQuantityKg;
            if (allocation.AllocatedQuantityKg <= 0)
            {
                allocation.AllocatedQuantityKg = effectiveQty;
            }
            if (allocation.Status == AllocationStatus.Lost)
            {
                allocation.Status = effectiveQty >= allocation.RequestedQuantityKg ? AllocationStatus.Won : AllocationStatus.PartiallyWon;
            }

            var order = new AuctionOrder
            {
                OrderNumber = orderNumber,
                AuctionId = payment.AuctionId,
                AuctionAllocationId = allocation.Id,
                AuctionPaymentId = payment.Id,
                CustomerProfileId = payment.CustomerProfileId,
                FarmerProfileId = payment.Auction.FarmerProfileId,
                CropId = payment.Auction.CropListing.CropId,
                AllocatedQuantityKg = effectiveQty,
                PricePerMan = allocation.WinningBidAmountPerMan,
                TotalAmount = totalAmount,
                Status = OrderStatus.Confirmed
            };

            dbContext.AuctionOrders.Add(order);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return order;
        });
    }
}
