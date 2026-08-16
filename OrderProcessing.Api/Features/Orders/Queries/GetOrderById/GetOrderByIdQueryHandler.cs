using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Exceptions;
using OrderProcessing.Api.Features.Orders.Queries.ReadModel;

namespace OrderProcessing.Api.Features.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderResponse>
{
    private readonly IOrderReadModelReader _reader;
    public GetOrderByIdQueryHandler(IOrderReadModelReader reader)
    {
        _reader = reader;
    }

    public async Task<OrderResponse> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var readModel = await _reader.GetByIdAsync(request.OrderId, cancellationToken);

        if (readModel is null)
        {
            throw new NotFoundException($"Order with id {request.OrderId} was not found.");
        }

        if (!Enum.TryParse<OrderStatus>(readModel.Status, ignoreCase: true, out var status))
        {
            throw new InvalidOperationException(
                $"Order read model {readModel.OrderId} " +
                $"contains invalid status '{readModel.Status}'.");
        }

        return new OrderResponse
        {
            Id = readModel.OrderId,
            CustomerId = readModel.CustomerId,
            CustomerName = readModel.CustomerName,
            Status = status,
            TotalAmount = readModel.TotalAmount,
            CreatedAtUtc = readModel.CreatedAtUtc,
            CompletedAtUtc = readModel.CompletedAtUtc,
            CancelledAtUtc = readModel.CancelledAtUtc,

            Items = readModel.Items
                .Select(item =>
                    new OrderItemResponse
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        LineTotal = item.LineTotal
                    })
                .ToList()
        };
    }
}