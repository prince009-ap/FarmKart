using System;

namespace FarmKart.Application.DTOs;

public sealed record InvoiceResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    DateTime InvoiceDateUtc,
    Guid OrderId,
    string OrderNumber,
    DateTime OrderDateUtc,
    string PaymentStatus,
    string PaymentReference,
    DateTime PaidAtUtc,
    string SellerName,
    string? SellerPhone,
    string? SellerLocation,
    string BuyerName,
    string? BuyerPhone,
    string FulfillmentMode,
    string? DeliveryOrPickupAddress,
    string CropName,
    string CropType,
    string Variety,
    string? PrimaryImageUrl,
    decimal QuantityKg,
    decimal QuantityMan,
    decimal PricePerMan,
    decimal SubtotalAmount,
    decimal TaxAmount,
    decimal TotalAmount
);
