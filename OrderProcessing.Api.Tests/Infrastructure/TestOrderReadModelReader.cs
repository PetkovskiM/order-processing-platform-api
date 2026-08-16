using OrderProcessing.Api.Features.Orders.Queries.ReadModel;
using OrderProcessing.ReadModels.Orders;

namespace OrderProcessing.Api.Tests.Infrastructure;

public sealed class TestOrderReadModelReader : IOrderReadModelReader
{
    public const int ExistingOrderId = 88001;

    public Task<OrderReadModel?> GetByIdAsync(int orderId, CancellationToken cancellationToken)
    {
        if (orderId != ExistingOrderId)
        {
            return Task.FromResult<OrderReadModel?>(null);
        }

        return Task.FromResult<OrderReadModel?>(new OrderReadModel
            {
                OrderId = ExistingOrderId,
                CustomerId =
                    TestDataSeeder.CustomerId,
                CustomerName =
                    "Integration Test Customer",
                Status = "Pending",
                TotalAmount = 24.99m,
                CreatedAtUtc =
                    new DateTime(
                        2026,
                        8,
                        1,
                        10,
                        0,
                        0,
                        DateTimeKind.Utc),

                LastUpdatedAtUtc =
                    new DateTime(
                        2026,
                        8,
                        1,
                        10,
                        0,
                        0,
                        DateTimeKind.Utc),

                Items =
                [
                    new OrderItemReadModel
                    {
                        ProductId =
                            TestDataSeeder.ProductId,
                        ProductName =
                            "Integration Test Product",
                        Quantity = 1,
                        UnitPrice = 24.99m,
                        LineTotal = 24.99m
                    }
                ]
            });
    }
}