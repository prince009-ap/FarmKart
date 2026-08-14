using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public sealed record CreateFarmerAuctionRequest(
    Guid CropId,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal Quantity,
    [Required, StringLength(20)] string Unit,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal StartingBidPrice,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal MinimumBidIncrement,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    [StringLength(1000)] string? Description);

public sealed record UpdateFarmerAuctionRequest(
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal Quantity,
    [Required, StringLength(20)] string Unit,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal StartingBidPrice,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal MinimumBidIncrement,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    [StringLength(1000)] string? Description);


public sealed record FarmerAuctionResponse(
    Guid Id, Guid CropId, string CropName, decimal Quantity, string Unit, decimal QuantityKg,
    decimal AvailableStockKg, decimal ReservedStockKg, decimal RemainingUnreservedStockKg,
    decimal StartingBidPrice, decimal MinimumBidIncrement, DateTime StartTimeUtc, DateTime EndTimeUtc,
    string Status, string? Description, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
