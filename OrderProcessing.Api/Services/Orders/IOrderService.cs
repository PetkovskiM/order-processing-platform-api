using OrderProcessing.Api.DTOs.Orders;

namespace OrderProcessing.Api.Services.Orders;

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);
}