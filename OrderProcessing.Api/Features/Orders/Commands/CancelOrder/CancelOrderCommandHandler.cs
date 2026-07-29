using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Exceptions;
using OrderProcessing.Api.Services.Auditing;
using OrderProcessing.Api.Services.Outbox;
using OrderProcessing.Contracts.Orders;

namespace OrderProcessing.Api.Features.Orders.Commands.CancelOrder;

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, OrderResponse>
{
    private readonly OrderProcessingDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    private readonly IOutboxWriter _outboxWriter;

    public CancelOrderCommandHandler(
        OrderProcessingDbContext dbContext,
        IAuditService auditService,
        ILogger<CancelOrderCommandHandler> logger,
        IOutboxWriter outboxWriter)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
        _outboxWriter = outboxWriter;
    }

    public async Task<OrderResponse> Handle(
        CancelOrderCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Cancelling order {OrderId}",
            request.OrderId);

        var order = await _dbContext.Orders
            .Include(order => order.Customer)
            .Include(order => order.Items)
                .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(order => order.Id == request.OrderId,cancellationToken)
                ?? throw new NotFoundException($"Order with id {request.OrderId} was not found.");

        if (order.Status != OrderStatus.Pending)
        {
            throw new BadRequestException(
                $"Only pending orders can be cancelled. Current status: {order.Status}.",
                ErrorCodes.InvalidOrderStatus);
        }

        var utcNow = DateTime.UtcNow;

        var oldValues = new
        {
            order.Id,
            order.Status,
            order.CompletedAtUtc,
            order.CancelledAtUtc
        };

        foreach (var item in order.Items)
        {
            item.Product.StockQuantity += item.Quantity;
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAtUtc = utcNow;

        _auditService.Add(
            entityName: nameof(Order),
            entityId: order.Id.ToString(),
            action: AuditActions.Cancelled,
            oldValues: oldValues,
            newValues: new
            {
                order.Id,
                order.Status,
                order.CompletedAtUtc,
                order.CancelledAtUtc
            },
            createdAtUtc: utcNow);

        var integrationEvent = CreateOrderCancelledIntegrationEvent(order, utcNow);

        _outboxWriter.Add(integrationEvent);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} cancelled and stock restored",
            order.Id);

        return MapToResponse(order);
    }

    private static OrderResponse MapToResponse(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CustomerName =
                $"{order.Customer.FirstName} {order.Customer.LastName}",
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
        };
    }

    private static OrderCancelledIntegrationEvent CreateOrderCancelledIntegrationEvent(Order order, DateTime cancelledAtUtc)
    {
        return new OrderCancelledIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAtUtc: cancelledAtUtc,
            OrderId: order.Id,
            CustomerId: order.CustomerId,
            CustomerName:
                $"{order.Customer.FirstName} {order.Customer.LastName}",
            CustomerEmail: order.Customer.Email,
            TotalAmount: order.TotalAmount,
            CancelledAtUtc: cancelledAtUtc);
    }
}