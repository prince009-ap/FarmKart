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

            if (payment.PaymentStatus != PaymentStatus.Paid)
            {
                throw new InvalidOperationException(
                    $"Order can only be created for PAID payments. Current status: {payment.PaymentStatus}.");
            }

            if (payment.AuctionOrder is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return MapOrderToResponse(payment.AuctionOrder, payment.Auction);
            }

            var allocation = payment.Auction.Allocations
                .FirstOrDefault(al =>
                    al.CustomerProfileId == payment.CustomerProfileId &&
                    al.AllocatedQuantityKg > 0 &&
                    (al.Status == AllocationStatus.Won || al.Status == AllocationStatus.PartiallyWon))
                ?? throw new InvalidOperationException(
                    "No winning or partially-won allocation found for this payment's customer on this auction.");

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

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(search) ||
                o.Crop.CropName.ToLower().Contains(search) ||
                (o.FarmerProfile.FullName != null && o.FarmerProfile.FullName.ToLower().Contains(search)) ||
                (o.FarmerProfile.FarmName != null && o.FarmerProfile.FarmName.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var statusStr = filter.Status.Trim();
            if (Enum.TryParse<OrderStatus>(statusStr, true, out var statusEnum))
            {
                query = query.Where(o => o.Status == statusEnum);
            }
        }

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
            .Include(o => o.AuctionAllocation)
            .Include(o => o.Auction)
                .ThenInclude(a => a.CropListing)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        if (order.CustomerProfileId != customerProfile.Id)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        var primaryImg = order.Crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
            ?? order.Crop.Images.FirstOrDefault()?.ImageUrl;

        var allocMan = AuctionPricingConstants.ConvertKgToMan(order.AllocatedQuantityKg);

        var reqKg = order.AuctionAllocation?.RequestedQuantityKg ?? order.AllocatedQuantityKg;
        var reqMan = AuctionPricingConstants.ConvertKgToMan(reqKg);

        var auctionKg = order.Auction?.CropListing != null
            ? CropStockUnitConverter.ToKilograms(order.Auction.CropListing.QuantityForSale, order.Auction.CropListing.Unit)
            : order.AllocatedQuantityKg;
        var auctionMan = AuctionPricingConstants.ConvertKgToMan(auctionKg);

        return new CustomerOrderDetailResponse(
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            AuctionId: order.AuctionId,
            CropId: order.CropId,
            CropName: order.Crop.CropName,
            CropType: order.Crop.CropType,
            Variety: order.Crop.Variety,
            PrimaryImageUrl: primaryImg,
            RequestedQuantityKg: reqKg,
            RequestedQuantityMan: reqMan,
            AllocatedQuantityKg: order.AllocatedQuantityKg,
            AllocatedQuantityMan: allocMan,
            PricePerMan: order.PricePerMan,
            TotalAmount: order.TotalAmount,
            FarmerName: order.FarmerProfile.FullName ?? order.FarmerProfile.FarmName ?? "Farmer",
            FarmLocation: order.FarmerProfile.FarmLocation,
            Status: order.Status.ToString().ToUpperInvariant(),
            PaymentStatus: order.AuctionPayment.PaymentStatus.ToString().ToUpperInvariant(),
            OrderDateUtc: order.CreatedAtUtc,
            AuctionStartTimeUtc: order.Auction?.StartTimeUtc ?? order.CreatedAtUtc,
            AuctionEndDateUtc: order.Auction?.EndTimeUtc ?? order.CreatedAtUtc,
            AuctionQuantityKg: auctionKg,
            AuctionQuantityMan: auctionMan,
            WinningBidAmount: order.PricePerMan,
            AuctionAllocationId: order.AuctionAllocationId,
            AuctionPaymentId: order.AuctionPaymentId,
            TransactionReference: order.AuctionPayment.TransactionReference,
            PaymentMethod: order.AuctionPayment.PaymentMethod.ToString().ToUpperInvariant(),
            PaidAtUtc: order.AuctionPayment.PaidAtUtc ?? order.CreatedAtUtc
        );
    }

    public async Task<FarmerOrderSummaryResponse> GetFarmerOrderSummaryAsync(
        Guid farmerUserId,
        CancellationToken cancellationToken = default)
    {
        var farmerProfile = await dbContext.FarmerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == farmerUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Farmer profile not found for authenticated user.");

        var orders = await dbContext.AuctionOrders
            .AsNoTracking()
            .Where(o => o.FarmerProfileId == farmerProfile.Id)
            .ToListAsync(cancellationToken);

        return new FarmerOrderSummaryResponse(
            TotalOrders: orders.Count,
            ConfirmedOrdersCount: orders.Count(o => o.Status == OrderStatus.Confirmed),
            ReadyForPickupCount: 0,
            PickedUpCount: 0,
            DeliveredCount: 0,
            CompletedCount: 0
        );
    }

    public async Task<IReadOnlyList<FarmerOrderListItemResponse>> GetFarmerOrdersAsync(
        Guid farmerUserId,
        FarmerOrderFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var farmerProfile = await dbContext.FarmerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == farmerUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Farmer profile not found for authenticated user.");

        var query = dbContext.AuctionOrders
            .AsNoTracking()
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .Include(o => o.CustomerProfile)
            .Include(o => o.AuctionPayment)
            .Where(o => o.FarmerProfileId == farmerProfile.Id);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(search) ||
                o.Crop.CropName.ToLower().Contains(search) ||
                (o.CustomerProfile.FullName != null && o.CustomerProfile.FullName.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var statusStr = filter.Status.Trim();
            if (Enum.TryParse<OrderStatus>(statusStr, true, out var statusEnum))
            {
                query = query.Where(o => o.Status == statusEnum);
            }
        }

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

            return new FarmerOrderListItemResponse(
                OrderId: o.Id,
                OrderNumber: o.OrderNumber,
                AuctionId: o.AuctionId,
                CropId: o.CropId,
                CropName: o.Crop.CropName,
                CropType: o.Crop.CropType,
                PrimaryImageUrl: primaryImg,
                CustomerName: o.CustomerProfile.FullName,
                AllocatedQuantityKg: o.AllocatedQuantityKg,
                AllocatedQuantityMan: allocMan,
                PricePerMan: o.PricePerMan,
                TotalAmount: o.TotalAmount,
                Status: o.Status.ToString().ToUpperInvariant(),
                PaymentStatus: o.AuctionPayment.PaymentStatus.ToString().ToUpperInvariant(),
                CreatedAtUtc: o.CreatedAtUtc
            );
        }).ToList();
    }

    public async Task<FarmerOrderDetailResponse> GetFarmerOrderDetailsAsync(
        Guid farmerUserId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var farmerProfile = await dbContext.FarmerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == farmerUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Farmer profile not found for authenticated user.");

        var order = await dbContext.AuctionOrders
            .AsNoTracking()
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .Include(o => o.CustomerProfile)
            .Include(o => o.AuctionPayment)
            .Include(o => o.AuctionAllocation)
            .Include(o => o.Auction)
                .ThenInclude(a => a.CropListing)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        if (order.FarmerProfileId != farmerProfile.Id)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        var primaryImg = order.Crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
            ?? order.Crop.Images.FirstOrDefault()?.ImageUrl;

        var allocMan = AuctionPricingConstants.ConvertKgToMan(order.AllocatedQuantityKg);

        var reqKg = order.AuctionAllocation?.RequestedQuantityKg ?? order.AllocatedQuantityKg;
        var reqMan = AuctionPricingConstants.ConvertKgToMan(reqKg);

        var auctionKg = order.Auction?.CropListing != null
            ? CropStockUnitConverter.ToKilograms(order.Auction.CropListing.QuantityForSale, order.Auction.CropListing.Unit)
            : order.AllocatedQuantityKg;
        var auctionMan = AuctionPricingConstants.ConvertKgToMan(auctionKg);

        return new FarmerOrderDetailResponse(
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            AuctionId: order.AuctionId,
            CropId: order.CropId,
            CropName: order.Crop.CropName,
            CropType: order.Crop.CropType,
            Variety: order.Crop.Variety,
            PrimaryImageUrl: primaryImg,
            CustomerName: order.CustomerProfile.FullName,
            CustomerPhone: order.CustomerProfile.Phone,
            CustomerCity: order.CustomerProfile.AddressInfo?.City,
            CustomerState: order.CustomerProfile.AddressInfo?.State,
            RequestedQuantityKg: reqKg,
            RequestedQuantityMan: reqMan,
            AllocatedQuantityKg: order.AllocatedQuantityKg,
            AllocatedQuantityMan: allocMan,
            PricePerMan: order.PricePerMan,
            TotalAmount: order.TotalAmount,
            AuctionQuantityKg: auctionKg,
            AuctionQuantityMan: auctionMan,
            WinningBidAmountPerMan: order.PricePerMan,
            AuctionStartTimeUtc: order.Auction?.StartTimeUtc ?? order.CreatedAtUtc,
            AuctionEndTimeUtc: order.Auction?.EndTimeUtc ?? order.CreatedAtUtc,
            Status: order.Status.ToString().ToUpperInvariant(),
            PaymentStatus: order.AuctionPayment.PaymentStatus.ToString().ToUpperInvariant(),
            OrderDateUtc: order.CreatedAtUtc,
            AuctionAllocationId: order.AuctionAllocationId,
            AuctionPaymentId: order.AuctionPaymentId,
            TransactionReference: order.AuctionPayment.TransactionReference,
            PaymentMethod: order.AuctionPayment.PaymentMethod.ToString().ToUpperInvariant(),
            PaidAtUtc: order.AuctionPayment.PaidAtUtc ?? order.CreatedAtUtc
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
