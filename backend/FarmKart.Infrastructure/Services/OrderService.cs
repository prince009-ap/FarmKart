using System.Data;
using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.Common;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class OrderService(FarmKartDbContext dbContext, INotificationService notificationService) : IOrderService
{
    public async Task<AuctionOrderResponse> CreateOrderFromPaidPaymentAsync(
        Guid paymentId,
        ProcessPaymentRequest? fulfillmentDetails = null,
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

            var customerProfile = await dbContext.CustomerProfiles
                .FirstOrDefaultAsync(c => c.Id == payment.CustomerProfileId, cancellationToken);

            var cropId = payment.Auction.CropListing.CropId;
            var farmerProfile = payment.Auction.FarmerProfile;

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

            var mode = FulfillmentMode.Delivery;
            if (!string.IsNullOrWhiteSpace(fulfillmentDetails?.FulfillmentMode) &&
                TryParseFulfillmentMode(fulfillmentDetails.FulfillmentMode, out var parsedMode))
            {
                mode = parsedMode;
            }

            var order = new AuctionOrder
            {
                OrderNumber = orderNumber,
                AuctionId = payment.AuctionId,
                AuctionAllocationId = allocation.Id,
                AuctionPaymentId = payment.Id,
                CustomerProfileId = payment.CustomerProfileId,
                FarmerProfileId = farmerProfile.Id,
                CropId = cropId,
                AllocatedQuantityKg = allocation.AllocatedQuantityKg,
                PricePerMan = allocation.WinningBidAmountPerMan,
                TotalAmount = expectedAmount,
                Status = OrderStatus.Confirmed,
                FulfillmentMode = mode
            };

            if (mode == FulfillmentMode.Delivery)
            {
                order.DeliveryAddress = fulfillmentDetails?.DeliveryAddress ?? customerProfile?.AddressInfo?.AddressLine;
                order.DeliveryCity = fulfillmentDetails?.DeliveryCity ?? customerProfile?.AddressInfo?.City;
                order.DeliveryState = fulfillmentDetails?.DeliveryState ?? customerProfile?.AddressInfo?.State;
                order.DeliveryPincode = fulfillmentDetails?.DeliveryPincode ?? customerProfile?.AddressInfo?.Pincode;
                order.ContactName = fulfillmentDetails?.ContactName ?? customerProfile?.FullName;
                order.ContactPhone = fulfillmentDetails?.ContactPhone ?? customerProfile?.Phone;
            }
            else
            {
                order.PickupLocation = farmerProfile.FarmLocation ?? farmerProfile.FarmName ?? farmerProfile.FullName;
                if (fulfillmentDetails?.PickupDate.HasValue == true)
                {
                    if (fulfillmentDetails.PickupDate.Value < DateTime.UtcNow.AddMinutes(-5))
                    {
                        throw new ArgumentException("Pickup date cannot be in the past.");
                    }
                    order.PickupDate = fulfillmentDetails.PickupDate.Value;
                }
            }

            dbContext.AuctionOrders.Add(order);
            await dbContext.SaveChangesAsync(cancellationToken);

            var history = new OrderStatusHistory
            {
                AuctionOrderId = order.Id,
                PreviousStatus = OrderStatus.Confirmed,
                NewStatus = OrderStatus.Confirmed,
                ChangedAtUtc = today,
                ChangedByUserId = customerProfile?.UserId.ToString() ?? "System",
                Note = "Order created after payment confirmation."
            };
            dbContext.OrderStatusHistories.Add(history);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Create Order Notifications
            var cropName = payment.Auction?.CropListing?.Crop?.CropName ?? "Crop";
            if (customerProfile != null && customerProfile.UserId != Guid.Empty)
            {
                await notificationService.CreateNotificationAsync(
                    recipientUserId: customerProfile.UserId.ToString(),
                    title: "Order Confirmed",
                    message: $"Your order #{orderNumber} for {cropName} has been confirmed.",
                    notificationType: NotificationType.OrderCreated,
                    relatedOrderId: order.Id,
                    relatedAuctionId: payment.AuctionId,
                    cancellationToken: cancellationToken);
            }

            if (farmerProfile != null && farmerProfile.UserId != Guid.Empty)
            {
                await notificationService.CreateNotificationAsync(
                    recipientUserId: farmerProfile.UserId.ToString(),
                    title: "Order Paid & Confirmed",
                    message: $"Order #{orderNumber} has been paid and confirmed.",
                    notificationType: NotificationType.AuctionOrderCreated,
                    relatedOrderId: order.Id,
                    relatedAuctionId: payment.AuctionId,
                    cancellationToken: cancellationToken);
            }

            // Execute automatic settlement on paid order creation
            await PerformSettlementLogicAsync(order.Id, cancellationToken);

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
                (o.FarmerProfile.FullName != null && o.FarmerProfile.FullName.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var statusStr = filter.Status.Trim();
            if (statusStr.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
            }
            else if (statusStr.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.Status == OrderStatus.Completed);
            }
            else if (Enum.TryParse<OrderStatus>(statusStr, true, out var statusEnum))
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
                FarmerName: o.FarmerProfile?.FullName ?? o.FarmerProfile?.FarmName ?? "Farmer",
                Status: FormatStatusString(o.Status),
                FulfillmentMode: o.FulfillmentMode.ToString().ToUpperInvariant(),
                PaymentStatus: o.AuctionPayment?.PaymentStatus.ToString().ToUpperInvariant() ?? "PAID",
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
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null || order.CustomerProfileId != customerProfile.Id)
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

        var timeline = order.StatusHistories
            .OrderBy(h => h.ChangedAtUtc)
            .Select(h => new OrderStatusHistoryResponse(
                HistoryId: h.Id,
                PreviousStatus: FormatStatusString(h.PreviousStatus),
                NewStatus: FormatStatusString(h.NewStatus),
                ChangedAtUtc: h.ChangedAtUtc,
                ChangedByUserId: h.ChangedByUserId,
                Note: h.Note
            )).ToList();

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
            FarmerName: order.FarmerProfile?.FullName ?? order.FarmerProfile?.FarmName ?? "Farmer",
            FarmLocation: order.FarmerProfile?.FarmLocation ?? "",
            Status: FormatStatusString(order.Status),
            FulfillmentMode: order.FulfillmentMode.ToString().ToUpperInvariant(),
            DeliveryAddress: order.DeliveryAddress,
            DeliveryCity: order.DeliveryCity,
            DeliveryState: order.DeliveryState,
            DeliveryPincode: order.DeliveryPincode,
            ContactName: order.ContactName,
            ContactPhone: order.ContactPhone,
            PickupLocation: order.PickupLocation ?? order.FarmerProfile?.FarmLocation ?? "",
            PickupDate: order.PickupDate,
            ExpectedDeliveryDate: order.ExpectedDeliveryDate,
            PaymentStatus: order.AuctionPayment?.PaymentStatus.ToString().ToUpperInvariant() ?? "PAID",
            IsSettled: order.IsSettled,
            SettlementStatus: order.IsSettled ? "SETTLED" : "NOT_SETTLED",
            OrderDateUtc: order.CreatedAtUtc,
            AuctionStartTimeUtc: order.Auction?.StartTimeUtc ?? order.CreatedAtUtc,
            AuctionEndDateUtc: order.Auction?.EndTimeUtc ?? order.CreatedAtUtc,
            AuctionQuantityKg: auctionKg,
            AuctionQuantityMan: auctionMan,
            WinningBidAmount: order.PricePerMan,
            AuctionAllocationId: order.AuctionAllocationId,
            AuctionPaymentId: order.AuctionPaymentId,
            TransactionReference: order.AuctionPayment?.TransactionReference ?? "",
            PaymentMethod: order.AuctionPayment?.PaymentMethod.ToString().ToUpperInvariant() ?? "CARD",
            PaidAtUtc: order.AuctionPayment?.PaidAtUtc ?? order.CreatedAtUtc,
            Timeline: timeline
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
            ReadyForPickupCount: orders.Count(o => o.Status == OrderStatus.ReadyForPickup),
            PickedUpCount: orders.Count(o => o.Status == OrderStatus.PickedUp || o.Status == OrderStatus.Dispatched),
            DeliveredCount: orders.Count(o => o.Status == OrderStatus.Delivered),
            CompletedCount: orders.Count(o => o.Status == OrderStatus.Completed)
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
            if (statusStr.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
            }
            else if (statusStr.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.Status == OrderStatus.Completed);
            }
            else if (Enum.TryParse<OrderStatus>(statusStr, true, out var statusEnum))
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
                FulfillmentMode: o.FulfillmentMode.ToString().ToUpperInvariant(),
                PickupDate: o.PickupDate,
                ExpectedDeliveryDate: o.ExpectedDeliveryDate,
                PaymentStatus: o.AuctionPayment?.PaymentStatus.ToString().ToUpperInvariant() ?? "PAID",
                IsSettled: o.IsSettled,
                SettlementStatus: o.IsSettled ? "SETTLED" : "NOT_SETTLED",
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
            .Include(o => o.FarmerProfile)
            .Include(o => o.CustomerProfile)
            .Include(o => o.AuctionPayment)
            .Include(o => o.AuctionAllocation)
            .Include(o => o.Auction)
                .ThenInclude(a => a.CropListing)
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null || order.FarmerProfileId != farmerProfile.Id)
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

        var timeline = order.StatusHistories
            .OrderBy(h => h.ChangedAtUtc)
            .Select(h => new OrderStatusHistoryResponse(
                HistoryId: h.Id,
                PreviousStatus: FormatStatusString(h.PreviousStatus),
                NewStatus: FormatStatusString(h.NewStatus),
                ChangedAtUtc: h.ChangedAtUtc,
                ChangedByUserId: h.ChangedByUserId,
                Note: h.Note
            )).ToList();

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
            Status: FormatStatusString(order.Status),
            FulfillmentMode: order.FulfillmentMode.ToString().ToUpperInvariant(),
            DeliveryAddress: order.DeliveryAddress,
            DeliveryCity: order.DeliveryCity,
            DeliveryState: order.DeliveryState,
            DeliveryPincode: order.DeliveryPincode,
            ContactName: order.ContactName,
            ContactPhone: order.ContactPhone,
            PickupLocation: order.PickupLocation ?? order.FarmerProfile?.FarmLocation ?? "",
            PickupDate: order.PickupDate,
            ExpectedDeliveryDate: order.ExpectedDeliveryDate,
            PaymentStatus: order.AuctionPayment?.PaymentStatus.ToString().ToUpperInvariant() ?? "PAID",
            IsSettled: order.IsSettled,
            SettlementStatus: order.IsSettled ? "SETTLED" : "NOT_SETTLED",
            OrderDateUtc: order.CreatedAtUtc,
            AuctionAllocationId: order.AuctionAllocationId,
            AuctionPaymentId: order.AuctionPaymentId,
            TransactionReference: order.AuctionPayment?.TransactionReference ?? "",
            PaymentMethod: order.AuctionPayment?.PaymentMethod.ToString().ToUpperInvariant() ?? "CARD",
            PaidAtUtc: order.AuctionPayment?.PaidAtUtc ?? order.CreatedAtUtc,
            Timeline: timeline
        );
    }

    public async Task<AuctionOrderResponse> UpdateOrderStatusAsync(
        Guid authenticatedUserId,
        Guid orderId,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.AuctionOrders
            .Include(o => o.Auction)
                .ThenInclude(a => a.CropListing)
                    .ThenInclude(l => l.Crop)
            .Include(o => o.FarmerProfile)
            .Include(o => o.CustomerProfile)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        var isFarmer = order.FarmerProfile.UserId == authenticatedUserId;
        var isCustomer = order.CustomerProfile.UserId == authenticatedUserId;

        if (!isFarmer && !isCustomer)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        if (!TryParseOrderStatus(request.NewStatus, out var nextStatus))
        {
            throw new ArgumentException($"Invalid status '{request.NewStatus}'.");
        }

        if (!isFarmer && nextStatus != OrderStatus.Completed)
        {
            throw new UnauthorizedAccessException("Customer is not authorized to perform farmer fulfillment actions.");
        }

        if (order.Status == OrderStatus.Completed)
        {
            throw new InvalidOperationException("Completed orders cannot be modified.");
        }

        if (!IsValidStatusTransition(order.Status, nextStatus, order.FulfillmentMode))
        {
            throw new InvalidOperationException(
                $"Invalid status transition from {order.Status} to {nextStatus} for {order.FulfillmentMode} order.");
        }

        var prevStatus = order.Status;
        order.Status = nextStatus;

        var history = new OrderStatusHistory
        {
            AuctionOrderId = order.Id,
            PreviousStatus = prevStatus,
            NewStatus = nextStatus,
            ChangedAtUtc = DateTime.UtcNow,
            ChangedByUserId = authenticatedUserId.ToString(),
            Note = request.Note
        };

        dbContext.OrderStatusHistories.Add(history);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Status Transition Notifications
        var orderNumber = order.OrderNumber;
        var custUserId = order.CustomerProfile.UserId.ToString();
        var farmerUserIdStr = order.FarmerProfile.UserId.ToString();

        switch (nextStatus)
        {
            case OrderStatus.Confirmed:
                await notificationService.CreateNotificationAsync(
                    custUserId,
                    "Order Confirmed",
                    $"Your order #{orderNumber} has been confirmed.",
                    NotificationType.OrderConfirmed,
                    relatedOrderId: order.Id,
                    relatedAuctionId: order.AuctionId,
                    cancellationToken: cancellationToken);
                break;

            case OrderStatus.ReadyForPickup:
                await notificationService.CreateNotificationAsync(
                    custUserId,
                    "Order Ready for Pickup",
                    $"Your order #{orderNumber} is ready for pickup.",
                    NotificationType.OrderReadyForPickup,
                    relatedOrderId: order.Id,
                    relatedAuctionId: order.AuctionId,
                    cancellationToken: cancellationToken);

                await notificationService.CreateNotificationAsync(
                    farmerUserIdStr,
                    "Order Ready for Fulfillment",
                    $"Order #{orderNumber} is ready for fulfillment.",
                    NotificationType.OrderReadyForPickup,
                    relatedOrderId: order.Id,
                    relatedAuctionId: order.AuctionId,
                    cancellationToken: cancellationToken);
                break;

            case OrderStatus.PickedUp:
                await notificationService.CreateNotificationAsync(
                    custUserId,
                    "Order Picked Up",
                    $"Your order #{orderNumber} has been picked up.",
                    NotificationType.OrderPickedUp,
                    relatedOrderId: order.Id,
                    relatedAuctionId: order.AuctionId,
                    cancellationToken: cancellationToken);
                break;

            case OrderStatus.Dispatched:
                await notificationService.CreateNotificationAsync(
                    custUserId,
                    "Order Dispatched",
                    $"Your order #{orderNumber} has been dispatched.",
                    NotificationType.OrderDispatched,
                    relatedOrderId: order.Id,
                    relatedAuctionId: order.AuctionId,
                    cancellationToken: cancellationToken);
                break;

            case OrderStatus.Delivered:
                await notificationService.CreateNotificationAsync(
                    custUserId,
                    "Order Delivered",
                    $"Your order #{orderNumber} has been delivered.",
                    NotificationType.OrderDelivered,
                    relatedOrderId: order.Id,
                    relatedAuctionId: order.AuctionId,
                    cancellationToken: cancellationToken);
                break;

            case OrderStatus.Completed:
                await notificationService.CreateNotificationAsync(
                    custUserId,
                    "Order Completed",
                    $"Your order #{orderNumber} has been completed.",
                    NotificationType.OrderCompleted,
                    relatedOrderId: order.Id,
                    relatedAuctionId: order.AuctionId,
                    cancellationToken: cancellationToken);

                await notificationService.CreateNotificationAsync(
                    farmerUserIdStr,
                    "Order Completed",
                    $"Order #{orderNumber} has been completed.",
                    NotificationType.OrderCompleted,
                    relatedOrderId: order.Id,
                    relatedAuctionId: order.AuctionId,
                    cancellationToken: cancellationToken);

                await PerformSettlementLogicAsync(order.Id, cancellationToken);
                break;
        }

        return MapOrderToResponse(order, order.Auction);
    }

    public async Task<CustomerOrderTrackingResponse> GetCustomerOrderTrackingAsync(
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
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null || order.CustomerProfileId != customerProfile.Id)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        var primaryImg = order.Crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
            ?? order.Crop.Images.FirstOrDefault()?.ImageUrl;

        var allocMan = AuctionPricingConstants.ConvertKgToMan(order.AllocatedQuantityKg);

        var timeline = order.StatusHistories
            .OrderBy(h => h.ChangedAtUtc)
            .Select(h => new OrderStatusHistoryResponse(
                HistoryId: h.Id,
                PreviousStatus: FormatStatusString(h.PreviousStatus),
                NewStatus: FormatStatusString(h.NewStatus),
                ChangedAtUtc: h.ChangedAtUtc,
                ChangedByUserId: h.ChangedByUserId,
                Note: h.Note
            )).ToList();

        var currentStatusStr = FormatStatusString(order.Status);
        var statusMessage = order.Status switch
        {
            OrderStatus.Confirmed => "Your order has been placed and payment confirmed by FarmKart.",
            OrderStatus.ReadyForPickup => order.FulfillmentMode == FulfillmentMode.Pickup
                ? "Your order is ready for pickup at the farm location."
                : "Your order is ready for pickup/dispatch.",
            OrderStatus.Dispatched => "Your order has been dispatched and is on its way to your delivery address.",
            OrderStatus.PickedUp => "Your order has been picked up successfully.",
            OrderStatus.Delivered => "Your order has been delivered to your location. Please confirm completion.",
            OrderStatus.Completed => "Order completed. Thank you for buying on FarmKart!",
            _ => "Order status updated."
        };

        return new CustomerOrderTrackingResponse(
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            AuctionId: order.AuctionId,
            CropName: order.Crop.CropName,
            CropType: order.Crop.CropType,
            Variety: order.Crop.Variety,
            PrimaryImageUrl: primaryImg,
            QuantityKg: order.AllocatedQuantityKg,
            QuantityMan: allocMan,
            FulfillmentMode: order.FulfillmentMode.ToString().ToUpperInvariant(),
            CurrentStatus: currentStatusStr,
            StatusMessage: statusMessage,
            FarmerName: order.FarmerProfile?.FullName ?? order.FarmerProfile?.FarmName ?? "Farmer",
            FarmLocation: order.FarmerProfile?.FarmLocation ?? "",
            DeliveryAddress: order.DeliveryAddress,
            DeliveryCity: order.DeliveryCity,
            DeliveryState: order.DeliveryState,
            DeliveryPincode: order.DeliveryPincode,
            ContactName: order.ContactName,
            ContactPhone: order.ContactPhone,
            PickupLocation: order.PickupLocation ?? order.FarmerProfile?.FarmLocation ?? "",
            PickupDate: order.PickupDate,
            ExpectedDeliveryDate: order.ExpectedDeliveryDate,
            OrderDateUtc: order.CreatedAtUtc,
            StatusHistory: timeline
        );
    }

    public async Task<OrderSettlementResponse> SettleOrderAsync(
        Guid authenticatedUserId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.AuctionOrders
            .Include(o => o.FarmerProfile)
            .Include(o => o.CustomerProfile)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        var isFarmer = order.FarmerProfile.UserId == authenticatedUserId;
        var isCustomer = order.CustomerProfile.UserId == authenticatedUserId;

        if (!isFarmer && !isCustomer)
        {
            throw new UnauthorizedAccessException("Only the order's farmer or customer can request order settlement.");
        }

        return await SettleOrderInternalCoreAsync(orderId, cancellationToken);
    }

    private async Task<OrderSettlementResponse> SettleOrderInternalCoreAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction != null)
        {
            return await PerformSettlementLogicAsync(orderId, cancellationToken);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var res = await PerformSettlementLogicAsync(orderId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return res;
        });
    }

    private async Task<OrderSettlementResponse> PerformSettlementLogicAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.AuctionOrders
            .Include(o => o.Crop)
            .Include(o => o.FarmerProfile)
            .Include(o => o.CustomerProfile)
            .Include(o => o.AuctionPayment)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");

        // Idempotent check
        var existingSettlement = await dbContext.OrderSettlements
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AuctionOrderId == order.Id, cancellationToken);

        if (existingSettlement != null || order.IsSettled)
        {
            var settId = existingSettlement?.Id ?? Guid.NewGuid();
            var settKg = existingSettlement?.SettledQuantityKg ?? order.AllocatedQuantityKg;
            var settAmt = existingSettlement?.SettledAmount ?? order.TotalAmount;
            var settStatus = existingSettlement?.SettlementStatus ?? "SETTLED";
            var settTime = existingSettlement?.SettledAtUtc ?? order.SettledAtUtc ?? DateTime.UtcNow;
            var settMan = AuctionPricingConstants.ConvertKgToMan(settKg);

            return new OrderSettlementResponse(
                SettlementId: settId,
                OrderId: order.Id,
                OrderNumber: order.OrderNumber,
                AuctionId: order.AuctionId,
                FarmerProfileId: order.FarmerProfileId,
                CustomerProfileId: order.CustomerProfileId,
                SettledQuantityKg: settKg,
                SettledQuantityMan: settMan,
                SettledAmount: settAmt,
                SettlementStatus: settStatus,
                SettledAtUtc: settTime
            );
        }

        if (order.AllocatedQuantityKg <= 0)
        {
            throw new InvalidOperationException("Cannot settle order with zero allocated quantity.");
        }

        var crop = order.Crop
            ?? throw new InvalidOperationException("Crop reference not found for order.");

        // Deduct stock safely (never result in negative stock)
        var qtyToDeduct = Math.Min(crop.Quantity, order.AllocatedQuantityKg);
        if (qtyToDeduct > 0)
        {
            crop.Quantity -= qtyToDeduct;
            var stockTx = new CropStockTransaction
            {
                CropId = crop.Id,
                Quantity = qtyToDeduct,
                Unit = MeasurementUnit.Kilogram,
                QuantityInBaseUnit = qtyToDeduct,
                TransactionType = CropStockTransactionType.Correction,
                Notes = $"Settlement for Order #{order.OrderNumber}"
            };
            dbContext.CropStockTransactions.Add(stockTx);
        }

        order.IsSettled = true;
        order.SettledAtUtc = DateTime.UtcNow;

        var settlement = new OrderSettlement
        {
            AuctionOrderId = order.Id,
            AuctionId = order.AuctionId,
            FarmerProfileId = order.FarmerProfileId,
            CustomerProfileId = order.CustomerProfileId,
            SettledQuantityKg = order.AllocatedQuantityKg,
            SettledAmount = order.TotalAmount,
            SettledAtUtc = DateTime.UtcNow,
            SettlementStatus = "SETTLED"
        };

        dbContext.OrderSettlements.Add(settlement);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Create Settlement Notification for Farmer
        await notificationService.CreateNotificationAsync(
            recipientUserId: order.FarmerProfile.UserId.ToString(),
            title: "Order Settled",
            message: $"Order #{order.OrderNumber} has been settled.",
            notificationType: NotificationType.SettlementCompleted,
            relatedOrderId: order.Id,
            relatedAuctionId: order.AuctionId,
            cancellationToken: cancellationToken);

        var settledMan = AuctionPricingConstants.ConvertKgToMan(settlement.SettledQuantityKg);

        return new OrderSettlementResponse(
            SettlementId: settlement.Id,
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            AuctionId: settlement.AuctionId,
            FarmerProfileId: settlement.FarmerProfileId,
            CustomerProfileId: settlement.CustomerProfileId,
            SettledQuantityKg: settlement.SettledQuantityKg,
            SettledQuantityMan: settledMan,
            SettledAmount: settlement.SettledAmount,
            SettlementStatus: settlement.SettlementStatus,
            SettledAtUtc: settlement.SettledAtUtc
        );
    }

    public async Task<CustomerOrderDetailResponse> UpdateCustomerOrderFulfillmentAsync(
        Guid customerUserId,
        Guid orderId,
        UpdateFulfillmentDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerProfile = await dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == customerUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Customer profile not found for authenticated user.");

        var order = await dbContext.AuctionOrders
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .Include(o => o.FarmerProfile)
            .Include(o => o.CustomerProfile)
            .Include(o => o.AuctionPayment)
            .Include(o => o.AuctionAllocation)
            .Include(o => o.Auction)
                .ThenInclude(a => a.CropListing)
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null || order.CustomerProfileId != customerProfile.Id)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Completed or cancelled orders cannot be modified.");
        }

        if (!TryParseFulfillmentMode(request.FulfillmentMode, out var newMode))
        {
            throw new ArgumentException($"Invalid fulfillment mode '{request.FulfillmentMode}'.");
        }

        if (newMode == FulfillmentMode.Pickup && request.PickupDate.HasValue && request.PickupDate.Value < DateTime.UtcNow.AddMinutes(-5))
        {
            throw new ArgumentException("Pickup date cannot be in the past.");
        }

        order.FulfillmentMode = newMode;
        if (newMode == FulfillmentMode.Delivery)
        {
            order.DeliveryAddress = request.DeliveryAddress ?? order.DeliveryAddress;
            order.DeliveryCity = request.DeliveryCity ?? order.DeliveryCity;
            order.DeliveryState = request.DeliveryState ?? order.DeliveryState;
            order.DeliveryPincode = request.DeliveryPincode ?? order.DeliveryPincode;
            order.ContactName = request.ContactName ?? order.ContactName;
            order.ContactPhone = request.ContactPhone ?? order.ContactPhone;
            order.ExpectedDeliveryDate = request.ExpectedDeliveryDate ?? order.ExpectedDeliveryDate;
        }
        else
        {
            order.PickupLocation = order.FarmerProfile.FarmLocation ?? order.FarmerProfile.FarmName ?? order.FarmerProfile.FullName;
            if (request.PickupDate.HasValue) order.PickupDate = request.PickupDate;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetCustomerOrderDetailsAsync(customerUserId, orderId, cancellationToken);
    }

    public static string FormatStatusString(OrderStatus status) => status switch
    {
        OrderStatus.ReadyForPickup => "READY_FOR_PICKUP",
        OrderStatus.PickedUp => "PICKED_UP",
        _ => status.ToString().ToUpperInvariant()
    };

    public static bool TryParseOrderStatus(string? statusStr, out OrderStatus result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(statusStr)) return false;

        var cleanStr = statusStr.Trim().Replace("_", "");
        return Enum.TryParse<OrderStatus>(cleanStr, true, out result);
    }

    public static bool TryParseFulfillmentMode(string? modeStr, out FulfillmentMode result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(modeStr)) return false;

        var cleanStr = modeStr.Trim().Replace("_", "");
        return Enum.TryParse<FulfillmentMode>(cleanStr, true, out result);
    }

    public static bool IsValidStatusTransition(OrderStatus current, OrderStatus next, FulfillmentMode mode)
    {
        if (current == OrderStatus.Completed) return false;
        if (current == next) return true;

        if (mode == FulfillmentMode.Delivery)
        {
            return (current, next) switch
            {
                (OrderStatus.Confirmed, OrderStatus.ReadyForPickup) => true,
                (OrderStatus.ReadyForPickup, OrderStatus.Dispatched) => true,
                (OrderStatus.Dispatched, OrderStatus.Delivered) => true,
                (OrderStatus.Delivered, OrderStatus.Completed) => true,
                _ => false
            };
        }
        else // Pickup
        {
            return (current, next) switch
            {
                (OrderStatus.Confirmed, OrderStatus.ReadyForPickup) => true,
                (OrderStatus.ReadyForPickup, OrderStatus.PickedUp) => true,
                (OrderStatus.PickedUp, OrderStatus.Completed) => true,
                _ => false
            };
        }
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
            Status: FormatStatusString(order.Status),
            FulfillmentMode: order.FulfillmentMode.ToString().ToUpperInvariant(),
            CreatedAtUtc: order.CreatedAtUtc
        );
    }

    public async Task<InvoiceResponse> GetOrCreateInvoiceForCustomerAsync(
        Guid customerUserId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var customerProfile = await dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == customerUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Customer profile not found for authenticated user.");

        var orderExists = await dbContext.AuctionOrders
            .AsNoTracking()
            .AnyAsync(o => o.Id == orderId && o.CustomerProfileId == customerProfile.Id, cancellationToken);

        if (!orderExists)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        return await GetOrCreateInvoiceInternalAsync(orderId, cancellationToken);
    }

    public async Task<InvoiceResponse> GetOrCreateInvoiceForFarmerAsync(
        Guid farmerUserId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var farmerProfile = await dbContext.FarmerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == farmerUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Farmer profile not found for authenticated user.");

        var orderExists = await dbContext.AuctionOrders
            .AsNoTracking()
            .AnyAsync(o => o.Id == orderId && o.FarmerProfileId == farmerProfile.Id, cancellationToken);

        if (!orderExists)
        {
            throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");
        }

        return await GetOrCreateInvoiceInternalAsync(orderId, cancellationToken);
    }

    private async Task<InvoiceResponse> GetOrCreateInvoiceInternalAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        // 1. Idempotency Check: Return existing Invoice if created
        var existingInvoice = await dbContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.AuctionOrderId == orderId, cancellationToken);

        if (existingInvoice != null)
        {
            return MapInvoiceToResponse(existingInvoice);
        }

        // 2. Fetch Order Details with Payment & Profiles
        var order = await dbContext.AuctionOrders
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .Include(o => o.FarmerProfile)
            .Include(o => o.CustomerProfile)
            .Include(o => o.AuctionPayment)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order with ID '{orderId}' was not found.");

        // 3. Verify Payment Status
        if (order.AuctionPayment == null || order.AuctionPayment.PaymentStatus != PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Invoice is available after successful payment.");
        }

        // 4. Generate Unique, Deterministic Invoice Number
        var invoiceNumber = order.OrderNumber.StartsWith("FK-")
            ? "INV-" + order.OrderNumber[3..]
            : $"INV-{DateTime.UtcNow:yyyyMMdd}-{order.Id.ToString()[..4].ToUpper()}";

        var primaryImg = order.Crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
            ?? order.Crop.Images.FirstOrDefault()?.ImageUrl;

        var quantityMan = AuctionPricingConstants.ConvertKgToMan(order.AllocatedQuantityKg);

        var addressList = new List<string>();
        if (order.FulfillmentMode == FulfillmentMode.Delivery)
        {
            if (!string.IsNullOrWhiteSpace(order.DeliveryAddress)) addressList.Add(order.DeliveryAddress);
            if (!string.IsNullOrWhiteSpace(order.DeliveryCity)) addressList.Add(order.DeliveryCity);
            if (!string.IsNullOrWhiteSpace(order.DeliveryState)) addressList.Add(order.DeliveryState);
            if (!string.IsNullOrWhiteSpace(order.DeliveryPincode)) addressList.Add(order.DeliveryPincode);
        }
        else
        {
            var pLoc = order.PickupLocation ?? order.FarmerProfile.FarmLocation ?? order.FarmerProfile.FarmName ?? order.FarmerProfile.FullName;
            if (!string.IsNullOrWhiteSpace(pLoc)) addressList.Add(pLoc);
        }
        var address = string.Join(", ", addressList);

        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            AuctionOrderId = order.Id,
            CustomerProfileId = order.CustomerProfileId,
            FarmerProfileId = order.FarmerProfileId,
            InvoiceDateUtc = DateTime.UtcNow,
            SellerName = order.FarmerProfile.FullName ?? order.FarmerProfile.FarmName ?? "FarmKart Seller",
            SellerPhone = order.FarmerProfile.Phone,
            SellerLocation = order.FarmerProfile.FarmLocation,
            BuyerName = order.ContactName ?? order.CustomerProfile.FullName ?? "FarmKart Buyer",
            BuyerPhone = order.ContactPhone ?? order.CustomerProfile.Phone,
            DeliveryOrPickupAddress = address,
            CropName = order.Crop.CropName,
            CropType = order.Crop.CropType,
            Variety = order.Crop.Variety ?? "",
            PrimaryImageUrl = primaryImg,
            QuantityKg = order.AllocatedQuantityKg,
            QuantityMan = quantityMan,
            PricePerMan = order.PricePerMan,
            SubtotalAmount = order.TotalAmount,
            TaxAmount = 0,
            TotalAmount = order.TotalAmount,
            PaymentStatus = "PAID",
            PaymentReference = order.AuctionPayment.TransactionReference,
            PaidAtUtc = order.AuctionPayment.PaidAtUtc ?? order.CreatedAtUtc,
            FulfillmentMode = order.FulfillmentMode.ToString().ToUpperInvariant()
        };

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapInvoiceToResponse(invoice);
    }

    private static InvoiceResponse MapInvoiceToResponse(Invoice invoice)
    {
        return new InvoiceResponse(
            InvoiceId: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            InvoiceDateUtc: invoice.InvoiceDateUtc,
            OrderId: invoice.AuctionOrderId,
            OrderNumber: invoice.AuctionOrder?.OrderNumber ?? invoice.InvoiceNumber.Replace("INV-", "FK-"),
            OrderDateUtc: invoice.AuctionOrder?.CreatedAtUtc ?? invoice.CreatedAtUtc,
            PaymentStatus: invoice.PaymentStatus,
            PaymentReference: invoice.PaymentReference,
            PaidAtUtc: invoice.PaidAtUtc,
            SellerName: invoice.SellerName,
            SellerPhone: invoice.SellerPhone,
            SellerLocation: invoice.SellerLocation,
            BuyerName: invoice.BuyerName,
            BuyerPhone: invoice.BuyerPhone,
            FulfillmentMode: invoice.FulfillmentMode,
            DeliveryOrPickupAddress: invoice.DeliveryOrPickupAddress,
            CropName: invoice.CropName,
            CropType: invoice.CropType,
            Variety: invoice.Variety,
            PrimaryImageUrl: invoice.PrimaryImageUrl,
            QuantityKg: invoice.QuantityKg,
            QuantityMan: invoice.QuantityMan,
            PricePerMan: invoice.PricePerMan,
            SubtotalAmount: invoice.SubtotalAmount,
            TaxAmount: invoice.TaxAmount,
            TotalAmount: invoice.TotalAmount
        );
    }
}
