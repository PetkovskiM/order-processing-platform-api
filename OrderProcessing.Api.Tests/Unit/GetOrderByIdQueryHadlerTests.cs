using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Exceptions;
using OrderProcessing.Api.Features.Orders.Queries.GetOrderById;
using OrderProcessing.Api.Features.Orders.Queries.ReadModel;
using OrderProcessing.ReadModels.Orders;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderProcessing.Api.Tests.Unit
{
    public class GetOrderByIdQueryHadlerTests
    {

        [Fact]
        public async Task Handle_WhenOrderExists_ReturnsMappedResponse()
        {
            // Arrange
            var readModel = new OrderReadModel
            {
                OrderId = 123,
                CustomerId = 456,
                CustomerName = "John Smith",
                Status = "Completed",
                TotalAmount = 99.99m,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                CompletedAtUtc = DateTime.UtcNow,
                LastUpdatedAtUtc = DateTime.UtcNow,

                Items =
                [
                    new OrderItemReadModel
            {
                ProductId = 10,
                ProductName = "Keyboard",
                Quantity = 1,
                UnitPrice = 99.99m,
                LineTotal = 99.99m
            }
                ]
            };

            var reader = new FakeOrderReadModelReader(readModel);

            var handler = new GetOrderByIdQueryHandler(reader);

            // Act
            var result = await handler.Handle(new GetOrderByIdQuery(123), CancellationToken.None);

            // Assert
            Assert.Equal(123, result.Id);
            Assert.Equal(456, result.CustomerId);
            Assert.Equal("John Smith", result.CustomerName);
            Assert.Equal(OrderStatus.Completed, result.Status);
            Assert.Equal(99.99m, result.TotalAmount);
            Assert.Single(result.Items);
        }

        [Fact]
        public async Task Handle_WhenOrderDoesNotExist_ThrowsNotFoundException()
        {
            var handler =
                new GetOrderByIdQueryHandler(
                    new FakeOrderReadModelReader(null));

            var action = () => handler.Handle(
                new GetOrderByIdQuery(999),
                CancellationToken.None);

            await Assert.ThrowsAsync<NotFoundException>(
                action);
        }

        private sealed class FakeOrderReadModelReader : IOrderReadModelReader
        {
            private readonly OrderReadModel? _order;

            public FakeOrderReadModelReader(OrderReadModel? order)
            {
                _order = order;
            }

            public Task<OrderReadModel?> GetByIdAsync(int orderId, CancellationToken cancellationToken)
            {
                return Task.FromResult(_order?.OrderId == orderId ? _order : null);
            }

            public Task<OrderReadModelPage> GetPageAsync(OrderQueryParameters parameters, CancellationToken cancellationToken)
            {
                return Task.FromResult(new OrderReadModelPage([], 0));
            }
        }
    }
}
