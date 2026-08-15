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

    public async Task<IReadOnlyList<CustomerOrderListItemResponse>> GetCustomerOrdersAsync(
        Guid customerUserId,
        CustomerOrderFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var customerProfile = await dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == customerUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Customer profile not found for authenticated user.");

        var query = dbContext.AuctionOrders
            .AsNoTracking()
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .Include(o => o.FarmerProfile)
            .Include(o => o.AuctionPayment)
            .Where(o => o.CustomerProfileId == customerProfile.Id);

        // Search filter (OrderNumber, CropName, FarmerName)
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(search) ||
                o.Crop.CropName.ToLower().Contains(search) ||
                (o.FarmerProfile.FullName != null && o.FarmerProfile.FullName.ToLower().Contains(search)) ||
                (o.FarmerProfile.FarmName != null && o.FarmerProfile.FarmName.ToLower().Contains(search)));
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var statusStr = filter.Status.Trim();
            if (Enum.TryParse<OrderStatus>(statusStr, true, out var statusEnum))
            {
                query = query.Where(o => o.Status == statusEnum);
            }
        }

        // Sort By (default: newest)
        if (filter.SortBy?.Trim().ToLower() == "oldest")
        {
            query = query.OrderBy(o => o.CreatedAtUtc);
        }
        else
        {
            query = query.OrderByDescending(o => o.CreatedAtUtc);
        }

        var orders = await query.ToListAsync(cancellationToken);

        return orders.Select(o =>
        {
            var primaryImg = o.Crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                ?? o.Crop.Images.FirstOrDefault()?.ImageUrl;

            var allocMan = AuctionPricingConstants.ConvertKgToMan(o.AllocatedQuantityKg);

            return new CustomerOrderListItemResponse(
                OrderId: o.Id,
                OrderNumber: o.OrderNumber,
                AuctionId: o.AuctionId,
                CropId: o.CropId,
                CropName: o.Crop.CropName,
                CropType: o.Crop.CropType,
                PrimaryImageUrl: primaryImg,
                AllocatedQuantityKg: o.AllocatedQuantityKg,
                AllocatedQuantityMan: allocMan,
                PricePerMan: o.PricePerMan,
                TotalAmount: o.TotalAmount,
                FarmerName: o.FarmerProfile.FullName ?? o.FarmerProfile.FarmName ?? "Farmer",
                Status: o.Status.ToString().ToUpperInvariant(),
                PaymentStatus: o.AuctionPayment.PaymentStatus.ToString().ToUpperInvariant(),
                CreatedAtUtc: o.CreatedAtUtc
            );
        }).ToList();
    }

    public async Task<CustomerOrderDetailResponse> GetCustomerOrderDetailsAsync(
        Guid customerUserId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var customerProfile = await dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == customerUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Customer profile not found for authenticated user.");

        var order = await dbContext.AuctionOrders
            .AsNoTracking()
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .Include(o => o.FarmerProfile)
            .Include(o => o.AuctionPayment)
            .Include(o => o.Auction)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        // Security check: order must belong to authenticated customer
        if (order.CustomerProfileId != customerProfile.Id)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        var primaryImg = order.Crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
            ?? order.Crop.Images.FirstOrDefault()?.ImageUrl;

        var allocMan = AuctionPricingConstants.ConvertKgToMan(order.AllocatedQuantityKg);

        return new CustomerOrderDetailResponse(
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            AuctionId: order.AuctionId,
            CropId: order.CropId,
            CropName: order.Crop.CropName,
            CropType: order.Crop.CropType,
            Variety: order.Crop.Variety,
            PrimaryImageUrl: primaryImg,
            AllocatedQuantityKg: order.AllocatedQuantityKg,
            AllocatedQuantityMan: allocMan,
            PricePerMan: order.PricePerMan,
            TotalAmount: order.TotalAmount,
            FarmerName: order.FarmerProfile.FullName ?? order.FarmerProfile.FarmName ?? "Farmer",
            FarmLocation: order.FarmerProfile.FarmLocation,
            Status: order.Status.ToString().ToUpperInvariant(),
            PaymentStatus: order.AuctionPayment.PaymentStatus.ToString().ToUpperInvariant(),
            OrderDateUtc: order.CreatedAtUtc,
            AuctionEndDateUtc: order.Auction.EndTimeUtc,
            WinningBidAmount: order.PricePerMan,
            AuctionAllocationId: order.AuctionAllocationId,
            AuctionPaymentId: order.AuctionPaymentId,
            TransactionReference: order.AuctionPayment.TransactionReference,
            PaymentMethod: order.AuctionPayment.PaymentMethod.ToString().ToUpperInvariant()
        );
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

