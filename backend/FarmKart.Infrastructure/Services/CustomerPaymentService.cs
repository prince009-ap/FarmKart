using System.Data;
using FarmKart.Application.Abstractions.Auctions;
using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.Abstractions.Payments;
using FarmKart.Application.Common;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class CustomerPaymentService(
    FarmKartDbContext dbContext,
    IPaymentProvider paymentProvider,
    IAuctionFinalizationService finalizationService,
    IOrderService orderService) : ICustomerPaymentService
{
    public async Task<AuctionPaymentResponse> ProcessAuctionPaymentAsync(
        Guid userId,
        Guid auctionId,
        ProcessPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerProfile = await dbContext.CustomerProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Customer profile not found for authenticated user.");

        var now = DateTime.UtcNow;

        var auctionCheck = await dbContext.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Auction with ID '{auctionId}' was not found.");

        if (now >= auctionCheck.EndTimeUtc)
        {
            await finalizationService.FinalizeExpiredAuctionsAsync(cancellationToken);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var auction = await dbContext.Auctions
                .Include(a => a.Allocations)
                    .ThenInclude(al => al.CustomerProfile)
                .Include(a => a.AuctionPayments)
                .Include(a => a.CropListing)
                    .ThenInclude(l => l.Crop)
                .Include(a => a.FarmerProfile)
                .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken)
                ?? throw new KeyNotFoundException($"Auction with ID '{auctionId}' was not found.");

            if (now < auction.EndTimeUtc && auction.AuctionStatus != AuctionStatus.Ended)
            {
                throw new InvalidOperationException("Payment is available only after an auction has ended.");
            }

            var custAllocation = auction.Allocations
                .FirstOrDefault(al => al.CustomerProfileId == customerProfile.Id && al.AllocatedQuantityKg > 0 && (al.Status == AllocationStatus.Won || al.Status == AllocationStatus.PartiallyWon))
                ?? auction.Allocations.FirstOrDefault(al => al.CustomerProfileId == customerProfile.Id && al.AllocatedQuantityKg > 0);

            if (custAllocation == null)
            {
                throw new UnauthorizedAccessException("Only winning customers with allocated quantity can pay for this auction.");
            }

            var existingPayment = await dbContext.AuctionPayments
                .FirstOrDefaultAsync(p => p.AuctionId == auctionId && p.CustomerProfileId == customerProfile.Id, cancellationToken);

            if (existingPayment != null && existingPayment.PaymentStatus == PaymentStatus.Paid)
            {
                // Return existing order if any (idempotency)
                var existingOrder = await orderService.GetOrderByPaymentIdAsync(existingPayment.Id, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return MapToResponse(existingPayment, auction, customerProfile.FullName, custAllocation.AllocatedQuantityKg, custAllocation.WinningBidAmountPerMan, existingOrder);
            }

            var allocatedMan = AuctionPricingConstants.ConvertKgToMan(custAllocation.AllocatedQuantityKg);
            var winningBidRate = custAllocation.WinningBidAmountPerMan;
            var totalPayableAmount = Math.Round(allocatedMan * winningBidRate, 2);

            var method = ParsePaymentMethod(request.PaymentMethod);
            var providerResult = await paymentProvider.ProcessPaymentAsync(totalPayableAmount, method, cancellationToken);

            AuctionPayment payment;
            if (existingPayment == null)
            {
                payment = new AuctionPayment
                {
                    AuctionId = auction.Id,
                    CustomerProfileId = customerProfile.Id,
                    Amount = totalPayableAmount,
                    AllocatedQuantityKg = custAllocation.AllocatedQuantityKg,
                    Currency = "INR",
                    PaymentMethod = method,
                    PaymentStatus = providerResult.IsSuccess ? PaymentStatus.Paid : PaymentStatus.Failed,
                    TransactionReference = providerResult.TransactionReference,
                    PaidAtUtc = providerResult.IsSuccess ? now : null
                };
                dbContext.AuctionPayments.Add(payment);
            }
            else
            {
                payment = existingPayment;
                payment.Amount = totalPayableAmount;
                payment.AllocatedQuantityKg = custAllocation.AllocatedQuantityKg;
                payment.PaymentMethod = method;
                payment.PaymentStatus = providerResult.IsSuccess ? PaymentStatus.Paid : PaymentStatus.Failed;
                payment.TransactionReference = providerResult.TransactionReference;
                payment.PaidAtUtc = providerResult.IsSuccess ? now : null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Auto-create order if payment succeeded
            AuctionOrderResponse? orderResponse = null;
            if (providerResult.IsSuccess)
            {
                try
                {
                    orderResponse = await orderService.CreateOrderFromPaidPaymentAsync(payment.Id, cancellationToken);
                }
                catch
                {
                    // Order creation failure must not roll back a successful payment
                    // It will be retried on the next call (idempotent)
                }
            }

            return MapToResponse(payment, auction, customerProfile.FullName, custAllocation.AllocatedQuantityKg, winningBidRate, orderResponse);
        });
    }

    public async Task<AuctionPaymentResponse> GetPaymentByIdAsync(
        Guid userId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var customerProfile = await dbContext.CustomerProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Customer profile not found.");

        var payment = await dbContext.AuctionPayments
            .Include(p => p.Auction)
                .ThenInclude(a => a.CropListing)
                    .ThenInclude(l => l.Crop)
            .Include(p => p.Auction)
                .ThenInclude(a => a.FarmerProfile)
            .Include(p => p.Auction)
                .ThenInclude(a => a.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment with ID '{paymentId}' was not found.");

        if (payment.CustomerProfileId != customerProfile.Id)
        {
            throw new UnauthorizedAccessException("You do not have permission to view this payment.");
        }

        var custAlloc = payment.Auction.Allocations
            .FirstOrDefault(al => al.CustomerProfileId == customerProfile.Id && (al.Status == AllocationStatus.Won || al.Status == AllocationStatus.PartiallyWon))
            ?? payment.Auction.Allocations.FirstOrDefault(al => al.CustomerProfileId == customerProfile.Id);
        var allocatedKg = payment.AllocatedQuantityKg > 0 ? payment.AllocatedQuantityKg : (custAlloc?.AllocatedQuantityKg ?? 0m);
        var winningBidRate = custAlloc?.WinningBidAmountPerMan ?? (payment.Amount > 0 && allocatedKg > 0 ? Math.Round(payment.Amount / (allocatedKg / 20m), 2) : payment.Amount);

        // Include order if payment is PAID
        AuctionOrderResponse? orderResponse = null;
        if (payment.PaymentStatus == PaymentStatus.Paid)
        {
            orderResponse = await orderService.GetOrderByPaymentIdAsync(payment.Id, cancellationToken);
        }

        return MapToResponse(payment, payment.Auction, customerProfile.FullName, allocatedKg, winningBidRate, orderResponse);
    }

    public async Task<IReadOnlyList<CustomerPaymentHistoryResponse>> GetCustomerPaymentHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var customerProfile = await dbContext.CustomerProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Customer profile not found.");

        var payments = await dbContext.AuctionPayments
            .AsNoTracking()
            .Include(p => p.Auction)
                .ThenInclude(a => a.CropListing)
                    .ThenInclude(l => l.Crop)
                        .ThenInclude(c => c.Images)
            .Include(p => p.Auction)
                .ThenInclude(a => a.Allocations)
            .Where(p => p.CustomerProfileId == customerProfile.Id)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return payments.Select(p =>
        {
            var crop = p.Auction.CropListing.Crop;
            var primaryImg = crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                ?? crop.Images.FirstOrDefault()?.ImageUrl;

            var custAlloc = p.Auction.Allocations
                .FirstOrDefault(al => al.CustomerProfileId == customerProfile.Id && (al.Status == AllocationStatus.Won || al.Status == AllocationStatus.PartiallyWon))
                ?? p.Auction.Allocations.FirstOrDefault(al => al.CustomerProfileId == customerProfile.Id);
            var allocKg = p.AllocatedQuantityKg > 0 ? p.AllocatedQuantityKg : (custAlloc?.AllocatedQuantityKg ?? 0m);
            var allocMan = AuctionPricingConstants.ConvertKgToMan(allocKg);
            var winningBidRate = custAlloc?.WinningBidAmountPerMan ?? (p.Amount > 0 && allocKg > 0 ? Math.Round(p.Amount / (allocKg / 20m), 2) : p.Amount);

            return new CustomerPaymentHistoryResponse(
                PaymentId: p.Id,
                AuctionId: p.AuctionId,
                CropId: crop.Id,
                CropName: crop.CropName,
                PrimaryImageUrl: primaryImg,
                CropType: crop.CropType,
                Quantity: p.Auction.CropListing.QuantityForSale,
                Unit: CropStockUnitConverter.Format(p.Auction.CropListing.Unit),
                QuantityMan: AuctionPricingConstants.ConvertKgToMan(CropStockUnitConverter.ToKilograms(p.Auction.CropListing.QuantityForSale, p.Auction.CropListing.Unit)),
                AllocatedQuantityKg: allocKg,
                AllocatedQuantityMan: allocMan,
                WinningBidAmount: winningBidRate,
                TotalPayableAmount: p.Amount,
                Currency: p.Currency,
                PaymentMethod: p.PaymentMethod.ToString().ToUpper(),
                PaymentStatus: p.PaymentStatus.ToString().ToUpper(),
                TransactionReference: p.TransactionReference,
                CreatedAtUtc: p.CreatedAtUtc,
                PaidAtUtc: p.PaidAtUtc
            );
        }).ToList();
    }

    public async Task<AuctionPaymentResponse?> GetFarmerAuctionPaymentAsync(
        Guid farmerUserId,
        Guid auctionId,
        CancellationToken cancellationToken = default)
    {
        var farmerProfile = await dbContext.FarmerProfiles
            .FirstOrDefaultAsync(f => f.UserId == farmerUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Farmer profile not found.");

        var auction = await dbContext.Auctions
            .AsNoTracking()
            .Include(a => a.Allocations)
                .ThenInclude(al => al.CustomerProfile)
            .Include(a => a.AuctionPayments)
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
            .Include(a => a.FarmerProfile)
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Auction with ID '{auctionId}' was not found.");

        if (auction.FarmerProfileId != farmerProfile.Id)
        {
            throw new UnauthorizedAccessException("Farmer does not own this auction.");
        }

        var payment = auction.AuctionPayments.FirstOrDefault();
        if (payment == null)
        {
            return null;
        }

        var custAlloc = auction.Allocations.FirstOrDefault(al => al.CustomerProfileId == payment.CustomerProfileId);
        var winnerName = custAlloc?.CustomerProfile?.FullName ?? "Winning Customer";
        var allocKg = payment.AllocatedQuantityKg > 0 ? payment.AllocatedQuantityKg : (custAlloc?.AllocatedQuantityKg ?? 0m);
        var winningBidRate = custAlloc?.WinningBidAmountPerMan ?? (payment.Amount > 0 && allocKg > 0 ? Math.Round(payment.Amount / (allocKg / 20m), 2) : payment.Amount);

        return MapToResponse(payment, auction, winnerName, allocKg, winningBidRate);
    }

    private static AuctionPaymentResponse MapToResponse(
        AuctionPayment payment,
        Auction auction,
        string winnerCustomerName,
        decimal allocatedKg,
        decimal winningBidRate,
        AuctionOrderResponse? order = null)
    {
        var crop = auction.CropListing.Crop;
        var totalQtyKg = CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit);
        var totalQtyMan = AuctionPricingConstants.ConvertKgToMan(totalQtyKg);
        var allocMan = AuctionPricingConstants.ConvertKgToMan(allocatedKg);

        return new AuctionPaymentResponse(
            PaymentId: payment.Id,
            AuctionId: auction.Id,
            CropId: crop.Id,
            CropName: crop.CropName,
            CropType: crop.CropType,
            Quantity: auction.CropListing.QuantityForSale,
            Unit: CropStockUnitConverter.Format(auction.CropListing.Unit),
            QuantityMan: totalQtyMan,
            AllocatedQuantityKg: allocatedKg,
            AllocatedQuantityMan: allocMan,
            WinningBidAmount: winningBidRate,
            TotalPayableAmount: payment.Amount,
            Currency: payment.Currency,
            PaymentMethod: payment.PaymentMethod.ToString().ToUpper(),
            PaymentStatus: payment.PaymentStatus.ToString().ToUpper(),
            TransactionReference: payment.TransactionReference,
            WinnerCustomerName: winnerCustomerName,
            FarmerName: auction.FarmerProfile?.FullName ?? "Farmer",
            CreatedAtUtc: payment.CreatedAtUtc,
            PaidAtUtc: payment.PaidAtUtc,
            ServerTimeUtc: DateTime.UtcNow,
            Order: order
        );
    }

    private static PaymentMethod ParsePaymentMethod(string methodStr)
    {
        return methodStr?.Trim().ToUpper() switch
        {
            "UPI" => PaymentMethod.Upi,
            "CARD" or "CREDIT_CARD" or "DEBIT_CARD" => PaymentMethod.BankTransfer,
            "NET_BANKING" or "NETBANKING" => PaymentMethod.Cash,
            _ => PaymentMethod.Other
        };
    }
}
