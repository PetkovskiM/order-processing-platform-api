using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Exceptions;

namespace OrderProcessing.Api.Services.Orders;

public class OrderService : IOrderService
{
    private readonly OrderProcessingDbContext _dbContext;

    public OrderService(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OrderResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            throw new BadRequestException("An order must contain at least one item.");
        }

        var duplicateProductIds = request.Items
            .GroupBy(i => i.ProductId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateProductIds.Count > 0)
        {
            throw new BadRequestException(
                $"Duplicate products are not allowed in the same order. Product ids: {string.Join(", ", duplicateProductIds)}.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var customer = await _dbContext.Customers
                .AsNoTracking()
                .Where(c => c.Id == request.CustomerId)
                .Select(c => new
                {
                    c.Id,
                    c.FirstName,
                    c.LastName
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException($"Customer with id {request.CustomerId} was not found.");

            var productIds = request.Items
                .Select(i => i.ProductId)
                .Distinct()
                .ToList();

            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var missingProductIds = productIds
                .Where(id => !products.ContainsKey(id))
                .ToList();

            if (missingProductIds.Count > 0)
            {
                throw new NotFoundException(
                    $"Products were not found: {string.Join(", ", missingProductIds)}.");
            }

            var insufficientStockItems = request.Items
                .Where(item => products[item.ProductId].StockQuantity < item.Quantity)
                .Select(item => new
                {
                    item.ProductId,
                    ProductName = products[item.ProductId].Name,
                    RequestedQuantity = item.Quantity,
                    AvailableQuantity = products[item.ProductId].StockQuantity
                })
                .ToList();

            if (insufficientStockItems.Count > 0)
            {
                var details = string.Join(
                    "; ",
                    insufficientStockItems.Select(item =>
                        $"{item.ProductName} (ProductId: {item.ProductId}) requested: {item.RequestedQuantity}, available: {item.AvailableQuantity}"));

                throw new BadRequestException($"Insufficient stock. {details}");
            }

            var utcNow = DateTime.UtcNow;

            var order = new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Pending,
                CreatedAtUtc = utcNow
            };

            foreach (var item in request.Items)
            {
                var product = products[item.ProductId];

                var lineTotal = product.Price * item.Quantity;

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    LineTotal = lineTotal
                });

                order.TotalAmount += lineTotal;

                product.StockQuantity -= item.Quantity;
                product.UpdatedAtUtc = utcNow;
            }

            _dbContext.Orders.Add(order);

            await _dbContext.SaveChangesAsync(cancellationToken);

            var auditLog = new AuditLog
            {
                EntityName = nameof(Order),
                EntityId = order.Id.ToString(),
                Action = "Created",
                OldValues = null,
                NewValues = JsonSerializer.Serialize(new
                {
                    order.Id,
                    order.CustomerId,
                    Status = order.Status.ToString(),
                    order.TotalAmount,
                    order.CreatedAtUtc,
                    Items = order.Items.Select(item => new
                    {
                        item.ProductId,
                        item.ProductName,
                        item.Quantity,
                        item.UnitPrice,
                        item.LineTotal
                    })
                }),
                CreatedAtUtc = utcNow
            };

            _dbContext.AuditLogs.Add(auditLog);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return MapToResponse(order, $"{customer.FirstName} {customer.LastName}");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static OrderResponse MapToResponse(Order order, string customerName)
    {
        return new OrderResponse
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CustomerName = customerName,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            CreatedAtUtc = order.CreatedAtUtc,
            CompletedAtUtc = order.CompletedAtUtc,
            CancelledAtUtc = order.CancelledAtUtc,
            Items = order.Items
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