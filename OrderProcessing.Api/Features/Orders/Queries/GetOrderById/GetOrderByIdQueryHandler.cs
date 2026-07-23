using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Exceptions;

namespace OrderProcessing.Api.Features.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderResponse>
{
    private readonly OrderProcessingDbContext _dbContext;

    public GetOrderByIdQueryHandler(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OrderResponse> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Id == request.OrderId)
            .Select(order => new OrderResponse
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                CustomerName =
                    order.Customer.FirstName + " " +
                    order.Customer.LastName,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                CreatedAtUtc = order.CreatedAtUtc,
                CompletedAtUtc = order.CompletedAtUtc,
                CancelledAtUtc = order.CancelledAtUtc,
                Items = order.Items
                    .OrderBy(item => item.Id)
                    .Select(item => new OrderItemResponse
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        LineTotal = item.LineTotal
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                $"Order with id {request.OrderId} was not found.");
    }
}