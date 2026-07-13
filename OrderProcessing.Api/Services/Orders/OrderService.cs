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
            .AsNoTracking()
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

        var order = new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
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
        }

        _dbContext.Orders.Add(order);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(order, $"{customer.FirstName} {customer.LastName}");
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