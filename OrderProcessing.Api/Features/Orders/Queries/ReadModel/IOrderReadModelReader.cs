using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.ReadModels.Orders;

namespace OrderProcessing.Api.Features.Orders.Queries.ReadModel;

public interface IOrderReadModelReader
{
    Task<OrderReadModel?> GetByIdAsync(int orderId, CancellationToken cancellationToken);

    Task<OrderReadModelPage> GetPageAsync(OrderQueryParameters parameters, CancellationToken cancellationToken);
}