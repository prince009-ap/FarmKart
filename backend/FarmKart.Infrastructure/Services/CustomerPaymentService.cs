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
    IAuctionFinalizationService finalizationService) : ICustomerPaymentService
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

        // Auto-finalize if expired but not finalized yet
        var auctionCheck = await dbContext.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Auction with ID '{auctionId}' was not found.");

        if (now >= auctionCheck.EndTimeUtc && auctionCheck.AuctionWinner == null)
        {
            await finalizationService.FinalizeExpiredAuctionsAsync(cancellationToken);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var auction = await dbContext.Auctions
                .Include(a => a.AuctionWinner)
                    .ThenInclude(w => w.CustomerProfile)
                .Include(a => a.AuctionPayment)
                .Include(a => a.CropListing)
                    .ThenInclude(l => l.Crop)
                .Include(a => a.FarmerProfile)
                .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken)
                ?? throw new KeyNotFoundException($"Auction with ID '{auctionId}' was not found.");

            if (now < auction.EndTimeUtc && auction.AuctionStatus != AuctionStatus.Ended)
            {
                throw new InvalidOperationException("Payment is available only after an auction has ended.");
            }

            if (auction.AuctionWinner == null)
            {
                throw new InvalidOperationException("This auction has no winner to accept payment.");
            }

            if (auction.AuctionWinner.CustomerProfileId != customerProfile.Id)
            {
                throw new UnauthorizedAccessException("Only the winning customer can pay for this auction.");
            }

            // Check if already paid (Idempotency)
            if (auction.AuctionPayment != null && auction.AuctionPayment.PaymentStatus == PaymentStatus.Paid)
            {
                await transaction.CommitAsync(cancellationToken);
                return MapToResponse(auction.AuctionPayment, auction, customerProfile.FullName);
            }

            var quantityInKg = CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit);
            var quantityInMan = AuctionPricingConstants.ConvertKgToMan(quantityInKg);
            var winningBidRate = auction.AuctionWinner.FinalAmount;
            var totalPayableAmount = Math.Round(quantityInMan * winningBidRate, 2);

            var method = ParsePaymentMethod(request.PaymentMethod);

            var providerResult = await paymentProvider.ProcessPaymentAsync(totalPayableAmount, method, cancellationToken);

            var payment = auction.AuctionPayment;
            if (payment == null)
            {
                payment = new AuctionPayment
                {
                    AuctionId = auction.Id,
                    CustomerProfileId = customerProfile.Id,
                    Amount = totalPayableAmount,
                    Currency = "INR",
                    PaymentMethod = method,
                    PaymentStatus = providerResult.IsSuccess ? PaymentStatus.Paid : PaymentStatus.Failed,
                    TransactionReference = providerResult.TransactionReference,
                    PaidAtUtc = providerResult.IsSuccess ? now : null
                };
                dbContext.AuctionPayments.Add(payment);
                auction.AuctionPayment = payment;
            }
            else
            {
                payment.Amount = totalPayableAmount;
                payment.PaymentMethod = method;
                payment.PaymentStatus = providerResult.IsSuccess ? PaymentStatus.Paid : PaymentStatus.Failed;
                payment.TransactionReference = providerResult.TransactionReference;
                payment.PaidAtUtc = providerResult.IsSuccess ? now : null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return MapToResponse(payment, auction, customerProfile.FullName);
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
                .ThenInclude(a => a.AuctionWinner)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment with ID '{paymentId}' was not found.");

        if (payment.CustomerProfileId != customerProfile.Id)
        {
            throw new UnauthorizedAccessException("You do not have permission to view this payment.");
        }

        return MapToResponse(payment, payment.Auction, customerProfile.FullName);
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
                .ThenInclude(a => a.AuctionWinner)
            .Where(p => p.CustomerProfileId == customerProfile.Id)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return payments.Select(p =>
        {
            var crop = p.Auction.CropListing.Crop;
            var primaryImg = crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                ?? crop.Images.FirstOrDefault()?.ImageUrl;
            var winningBidRate = p.Auction.AuctionWinner?.FinalAmount ?? p.Amount;

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
            .Include(a => a.AuctionWinner)
                .ThenInclude(w => w.CustomerProfile)
            .Include(a => a.AuctionPayment)
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
            .Include(a => a.FarmerProfile)
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Auction with ID '{auctionId}' was not found.");

        if (auction.FarmerProfileId != farmerProfile.Id)
        {
            throw new UnauthorizedAccessException("Farmer does not own this auction.");
        }

        if (auction.AuctionPayment == null)
        {
            return null;
        }

        var winnerName = auction.AuctionWinner?.CustomerProfile?.FullName ?? "Winning Customer";

        return MapToResponse(auction.AuctionPayment, auction, winnerName);
    }

    private static AuctionPaymentResponse MapToResponse(AuctionPayment payment, Auction auction, string winnerCustomerName)
    {
        var crop = auction.CropListing.Crop;
        var winningBidRate = auction.AuctionWinner?.FinalAmount ?? payment.Amount;
        var qtyKg = CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit);
        var qtyMan = AuctionPricingConstants.ConvertKgToMan(qtyKg);

        return new AuctionPaymentResponse(
            PaymentId: payment.Id,
            AuctionId: auction.Id,
            CropId: crop.Id,
            CropName: crop.CropName,
            CropType: crop.CropType,
            Quantity: auction.CropListing.QuantityForSale,
            Unit: CropStockUnitConverter.Format(auction.CropListing.Unit),
            QuantityMan: qtyMan,
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
            ServerTimeUtc: DateTime.UtcNow
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
