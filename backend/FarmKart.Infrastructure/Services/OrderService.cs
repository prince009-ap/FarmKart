using System.Data;
using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.Common;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class OrderService(FarmKartDbContext dbContext) : IOrderService
{
    public async Task<AuctionOrderResponse> CreateOrderFromPaidPaymentAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            // Load payment with all required relationships
            var payment = await dbContext.AuctionPayments
                .Include(p => p.Auction)
                    .ThenInclude(a => a.CropListing)
                        .ThenInclude(l => l.Crop)
                .Include(p => p.Auction)
                    .ThenInclude(a => a.FarmerProfile)
                .Include(p => p.Auction)
                    .ThenInclude(a => a.Allocations)
                .Include(p => p.AuctionOrder)
                .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken)
                ?? throw new KeyNotFoundException($"Payment with ID '{paymentId}' was not found.");

            // Verify payment is PAID
            if (payment.PaymentStatus != PaymentStatus.Paid)
            {
                throw new InvalidOperationException(
                    $"Order can only be created for PAID payments. Current status: {payment.PaymentStatus}.");
            }

            // Idempotency: return existing order if already created
            if (payment.AuctionOrder is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return MapOrderToResponse(payment.AuctionOrder, payment.Auction);
            }

            // Find the AuctionAllocation for this customer and auction
            var allocation = payment.Auction.Allocations
                .FirstOrDefault(al =>
                    al.CustomerProfileId == payment.CustomerProfileId &&
                    al.AllocatedQuantityKg > 0 &&
                    (al.Status == AllocationStatus.Won || al.Status == AllocationStatus.PartiallyWon))
                ?? throw new InvalidOperationException(
                    "No winning or partially-won allocation found for this payment's customer on this auction.");

            // Validate amount: AllocatedQuantityKg / 20 * PricePerMan
            var expectedAmount = Math.Round(
                AuctionPricingConstants.ConvertKgToMan(allocation.AllocatedQuantityKg) * allocation.WinningBidAmountPerMan, 2);

            if (Math.Abs(payment.Amount - expectedAmount) > 0.01m)
            {
                throw new InvalidOperationException(
                    $"Payment amount ₹{payment.Amount} does not match expected amount ₹{expectedAmount} " +
                    $"({allocation.AllocatedQuantityKg} Kg @ ₹{allocation.WinningBidAmountPerMan}/Man).");
            }

            var cropId = payment.Auction.CropListing.CropId;
            var farmerProfileId = payment.Auction.FarmerProfileId;

            // Generate unique OrderNumber: FK-YYYYMMDD-NNNN
            var today = DateTime.UtcNow;
            var dateStr = today.ToString("yyyyMMdd");
            var todayStart = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0, DateTimeKind.Utc);
            var todayEnd = todayStart.AddDays(1);
            var countToday = await dbContext.AuctionOrders
                .CountAsync(o => o.CreatedAtUtc >= todayStart && o.CreatedAtUtc < todayEnd, cancellationToken);
            var seqNumber = (countToday + 1).ToString("D4");
            var orderNumber = $"FK-{dateStr}-{seqNumber}";

            // Ensure uniqueness (handle race condition by appending milliseconds if needed)
            var exists = await dbContext.AuctionOrders.AnyAsync(o => o.OrderNumber == orderNumber, cancellationToken);
            if (exists)
            {
                orderNumber = $"FK-{dateStr}-{today:HHmmss}-{seqNumber}";
            }

            var order = new AuctionOrder
            {
                OrderNumber = orderNumber,
                AuctionId = payment.AuctionId,
                AuctionAllocationId = allocation.Id,
                AuctionPaymentId = payment.Id,
                CustomerProfileId = payment.CustomerProfileId,
                FarmerProfileId = farmerProfileId,
                CropId = cropId,
                AllocatedQuantityKg = allocation.AllocatedQuantityKg,
                PricePerMan = allocation.WinningBidAmountPerMan,
                TotalAmount = expectedAmount,
                Status = OrderStatus.Confirmed
            };

            dbContext.AuctionOrders.Add(order);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return MapOrderToResponse(order, payment.Auction);
        });
    }

    public async Task<AuctionOrderResponse?> GetOrderByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.AuctionOrders
            .AsNoTracking()
            .Include(o => o.Auction)
                .ThenInclude(a => a.CropListing)
                    .ThenInclude(l => l.Crop)
            .FirstOrDefaultAsync(o => o.AuctionPaymentId == paymentId, cancellationToken);

        if (order is null) return null;

        return MapOrderToResponse(order, order.Auction);
    }

    private static AuctionOrderResponse MapOrderToResponse(AuctionOrder order, Auction auction)
    {
        var crop = auction.CropListing.Crop;
        var allocatedMan = AuctionPricingConstants.ConvertKgToMan(order.AllocatedQuantityKg);

        return new AuctionOrderResponse(
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            AuctionId: order.AuctionId,
            AuctionPaymentId: order.AuctionPaymentId,
            AuctionAllocationId: order.AuctionAllocationId,
            CropName: crop.CropName,
            CropType: crop.CropType,
            AllocatedQuantityKg: order.AllocatedQuantityKg,
            AllocatedQuantityMan: allocatedMan,
            PricePerMan: order.PricePerMan,
            TotalAmount: order.TotalAmount,
            Status: order.Status.ToString().ToUpperInvariant(),
            CreatedAtUtc: order.CreatedAtUtc
        );
    }
}
