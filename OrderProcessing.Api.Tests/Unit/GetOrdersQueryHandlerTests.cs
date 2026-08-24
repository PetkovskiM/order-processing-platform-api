using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Exceptions;
using OrderProcessing.Api.Features.Orders.Queries.GetOrders;
using OrderProcessing.Api.Features.Orders.Queries.ReadModel;
using OrderProcessing.ReadModels.Orders;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderProcessing.Api.Tests.Unit
{
    public class GetOrdersQueryHandlerTests
    {
        [Fact]
        public async Task Handle_WhenOrdersExist_ReturnsMappedPagedResponse()
        {
            var parameters = new OrderQueryParameters
            {
                Page = 2,
                PageSize = 5
            };

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

            var reader = new FakeOrderReadModelReader(
                new OrderReadModelPage([readModel], 8));

            var handler = new GetOrdersQueryHandler(reader);

            var result = await handler.Handle(
                new GetOrdersQuery(parameters),
                CancellationToken.None);

            Assert.Equal(2, result.Page);
            Assert.Equal(5, result.PageSize);
            Assert.Equal(8, result.TotalCount);
            Assert.Equal(2, result.TotalPages);

            var order = Assert.Single(result.Items);

            Assert.Equal(123, order.Id);
            Assert.Equal("John Smith", order.CustomerName);
            Assert.Equal(OrderStatus.Completed, order.Status);
            Assert.Single(order.Items);

            Assert.Same(parameters, reader.ReceivedParameters);
        }

        private sealed class FakeOrderReadModelReader : IOrderReadModelReader
        {
            private readonly OrderReadModelPage _page;

            public FakeOrderReadModelReader(OrderReadModelPage page)
            {
                _page = page;
            }

            public OrderQueryParameters? ReceivedParameters { get; private set; }

            public Task<OrderReadModel?> GetByIdAsync(int orderId, CancellationToken cancellationToken)
            {
                return Task.FromResult<OrderReadModel?>(null);
            }

            public Task<OrderReadModelPage> GetPageAsync(OrderQueryParameters parameters, CancellationToken cancellationToken)
            {
                ReceivedParameters = parameters;

                return Task.FromResult(_page);
            }
        }

        [Fact]
        public async Task Handle_WhenDateRangeIsInvalid_ThrowsBadRequestException()
        {
            var reader = new FakeOrderReadModelReader(new OrderReadModelPage([], 0));

            var handler = new GetOrdersQueryHandler(reader);

            var parameters = new OrderQueryParameters
            {
                CreatedFromUtc = new DateTime(2026, 8, 15),
                CreatedToUtc = new DateTime(2026, 8, 14)
            };

            var action = () => handler.Handle( new GetOrdersQuery(parameters), CancellationToken.None);

            await Assert.ThrowsAsync<BadRequestException>(action);

            Assert.Null(reader.ReceivedParameters);
        }

    }

}
