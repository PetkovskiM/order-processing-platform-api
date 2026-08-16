using OrderProcessing.ReadModels.Orders;

namespace OrderProcessing.Api.Features.Orders.Queries.ReadModel;

public interface IOrderReadModelReader
{
    Task<OrderReadModel?> GetByIdAsync(int orderId, CancellationToken cancellationToken);
}