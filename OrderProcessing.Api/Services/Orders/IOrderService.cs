using OrderProcessing.Api.DTOs.Common;
using OrderProcessing.Api.DTOs.Orders;

namespace OrderProcessing.Api.Services.Orders;

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<OrderResponse>> GetAllAsync(
    OrderQueryParameters parameters,
    CancellationToken cancellationToken = default);

    Task<OrderResponse> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<OrderResponse> CompleteAsync(
       int id,
       CancellationToken cancellationToken = default);

    Task<OrderResponse> CancelAsync(
        int id,
        CancellationToken cancellationToken = default);
}