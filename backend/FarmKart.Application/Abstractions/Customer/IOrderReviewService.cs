using FarmKart.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Customer;

public interface IOrderReviewService
{
    Task<OrderReviewResponse> CreateOrderReviewAsync(string customerUserId, Guid orderId, CreateOrderReviewRequest request);
    Task<OrderReviewResponse?> GetOrderReviewForCustomerAsync(string customerUserId, Guid orderId);
    Task<OrderReviewResponse> UpdateOrderReviewAsync(string customerUserId, Guid orderId, UpdateOrderReviewRequest request);
    Task<IReadOnlyList<OrderReviewResponse>> GetCustomerReviewsAsync(string customerUserId);
    Task<OrderReviewResponse?> GetOrderReviewForFarmerAsync(string farmerUserId, Guid orderId);
    Task<FarmerRatingSummaryResponse> GetFarmerRatingSummaryAsync(string farmerUserId);
}
