using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Exceptions;
using OrderProcessing.Api.Services.Auditing;
using OrderProcessing.Api.Services.Emailing;

namespace OrderProcessing.Api.Features.Orders.Commands.CompleteOrder;

public sealed class CompleteOrderCommandHandler
    : IRequestHandler<CompleteOrderCommand, OrderResponse>
{
    private readonly OrderProcessingDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<CompleteOrderCommandHandler> _logger;

    public CompleteOrderCommandHandler(
        OrderProcessingDbContext dbContext,
        IAuditService auditService,
        IEmailQueue emailQueue,
        ILogger<CompleteOrderCommandHandler> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _emailQueue = emailQueue;
        _logger = logger;
    }

    public async Task<OrderResponse> Handle(
        CompleteOrderCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(order => order.Customer)
            .Include(order => order.Items)
            .FirstOrDefaultAsync(
                order => order.Id == request.OrderId,
                cancellationToken)
            ?? throw new NotFoundException(
                $"Order with id {request.OrderId} was not found.");

        if (order.Status != OrderStatus.Pending)
        {
            throw new BadRequestException(
                $"Only pending orders can be completed. Current status: {order.Status}.",
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

        order.Status = OrderStatus.Completed;
        order.CompletedAtUtc = utcNow;

        _auditService.Add(
            entityName: nameof(Order),
            entityId: order.Id.ToString(),
            action: AuditActions.Completed,
            oldValues: oldValues,
            newValues: new
            {
                order.Id,
                order.Status,
                order.CompletedAtUtc,
                order.CancelledAtUtc
            },
            createdAtUtc: utcNow);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} completed",
            order.Id);

        var emailMessage = CreateOrderCompletedEmail(order);

        await TryEnqueueEmailAsync(
            emailMessage,
            order.Id,
            cancellationToken);

        return MapToResponse(order);
    }

    private static EmailMessage CreateOrderCompletedEmail(
        Order order)
    {
        var customerName =
            $"{order.Customer.FirstName} {order.Customer.LastName}";

        var itemLines = order.Items.Select(item =>
            $"- {item.ProductName}: {item.Quantity} × " +
            $"{item.UnitPrice:F2} = {item.LineTotal:F2}");

        var body = $"""
            Hello {customerName},

            Your order #{order.Id} has been completed successfully.

            Items:
            {string.Join(Environment.NewLine, itemLines)}

            Total amount: {order.TotalAmount:F2}
            Status: {order.Status}
            Completed at: {order.CompletedAtUtc:yyyy-MM-dd HH:mm} UTC

            Thank you.
            """;

        return new EmailMessage
        {
            To = order.Customer.Email,
            Subject = $"Order #{order.Id} completed",
            Body = body,
            IsHtml = false
        };
    }

    private async Task TryEnqueueEmailAsync(
        EmailMessage message,
        int orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailQueue.EnqueueAsync(
                message,
                cancellationToken);

            _logger.LogInformation(
                "Order-completed email queued. " +
                "OrderId: {OrderId}, Recipient: {Recipient}",
                orderId,
                message.To);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Order {OrderId} was completed, but email " +
                "enqueueing was cancelled",
                orderId);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Order {OrderId} was completed, but its email " +
                "could not be queued",
                orderId);
        }
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
}