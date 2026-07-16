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
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrderProcessingDbContext dbContext, ILogger<OrderService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating order for customer {CustomerId} with {ItemCount} items",
            request.CustomerId,
            request.Items.Count);

        ValidateCreateOrderRequest(request);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var customer = await GetCustomerSummaryAsync(request.CustomerId, cancellationToken);

            var productIds = request.Items
                .Select(i => i.ProductId)
                .Distinct()
                .ToList();

            var products = await GetProductsByIdAsync(productIds, cancellationToken);

            ValidateProductsExist(productIds, products);

            ValidateStockAvailability(request.Items, products);

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

            AddAuditLog(
             nameof(Order),
             order.Id.ToString(),
             "Created",
             null,
             new
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
             },
             utcNow);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
             "Order {OrderId} created for customer {CustomerId} with total amount {TotalAmount}",
             order.Id,
             order.CustomerId,
             order.TotalAmount);

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

    public async Task<IReadOnlyList<OrderResponse>> GetAllAsync(
     CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new OrderResponse
            {
                Id = o.Id,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer.FirstName + " " + o.Customer.LastName,
                Status = o.Status,
                TotalAmount = o.TotalAmount,
                CreatedAtUtc = o.CreatedAtUtc,
                CompletedAtUtc = o.CompletedAtUtc,
                CancelledAtUtc = o.CancelledAtUtc,
                Items = o.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new OrderItemResponse
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        LineTotal = i.LineTotal
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<OrderResponse> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrderResponse
            {
                Id = o.Id,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer.FirstName + " " + o.Customer.LastName,
                Status = o.Status,
                TotalAmount = o.TotalAmount,
                CreatedAtUtc = o.CreatedAtUtc,
                CompletedAtUtc = o.CompletedAtUtc,
                CancelledAtUtc = o.CancelledAtUtc,
                Items = o.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new OrderItemResponse
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        LineTotal = i.LineTotal
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Order with id {id} was not found.");
    }

    public async Task<OrderResponse> CompleteAsync(
    int id,
    CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Order with id {id} was not found.");

        if (order.Status != OrderStatus.Pending)
        {
            throw new BadRequestException(
                $"Only pending orders can be completed. Current status: {order.Status}.");
        }

        var utcNow = DateTime.UtcNow;

        var oldValues = JsonSerializer.Serialize(new
        {
            order.Id,
            Status = order.Status.ToString(),
            order.CompletedAtUtc,
            order.CancelledAtUtc
        });

        order.Status = OrderStatus.Completed;
        order.CompletedAtUtc = utcNow;

        var auditLog = new AuditLog
        {
            EntityName = nameof(Order),
            EntityId = order.Id.ToString(),
            Action = "Completed",
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new
            {
                order.Id,
                Status = order.Status.ToString(),
                order.CompletedAtUtc,
                order.CancelledAtUtc
            }),
            CreatedAtUtc = utcNow
        };

        _dbContext.AuditLogs.Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
         "Order {OrderId} completed",
         order.Id);

        return MapToResponse(order, $"{order.Customer.FirstName} {order.Customer.LastName}");
    }

    public async Task<OrderResponse> CancelAsync(
    int id,
    CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Order with id {id} was not found.");

        if (order.Status != OrderStatus.Pending)
        {
            throw new BadRequestException(
                $"Only pending orders can be cancelled. Current status: {order.Status}.");
        }

        var productIds = order.Items
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var utcNow = DateTime.UtcNow;

        var oldValues = JsonSerializer.Serialize(new
        {
            order.Id,
            Status = order.Status.ToString(),
            order.CompletedAtUtc,
            order.CancelledAtUtc,
            RestoredStock = order.Items.Select(item => new
            {
                item.ProductId,
                item.ProductName,
                item.Quantity
            })
        });

        foreach (var item in order.Items)
        {
            if (products.TryGetValue(item.ProductId, out var product))
            {
                product.StockQuantity += item.Quantity;
                product.UpdatedAtUtc = utcNow;
            }
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAtUtc = utcNow;

        var auditLog = new AuditLog
        {
            EntityName = nameof(Order),
            EntityId = order.Id.ToString(),
            Action = "Cancelled",
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new
            {
                order.Id,
                Status = order.Status.ToString(),
                order.CompletedAtUtc,
                order.CancelledAtUtc,
                RestoredStock = order.Items.Select(item => new
                {
                    item.ProductId,
                    item.ProductName,
                    item.Quantity
                })
            }),
            CreatedAtUtc = utcNow
        };

        _dbContext.AuditLogs.Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
         "Order {OrderId} cancelled and stock restored",
         order.Id);

        return MapToResponse(order, $"{order.Customer.FirstName} {order.Customer.LastName}");
    }

    private static void ValidateCreateOrderRequest(CreateOrderRequest request)
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
    }

    private sealed record CustomerSummary(
    int Id,
    string FirstName,
    string LastName);

    private async Task<CustomerSummary> GetCustomerSummaryAsync(
    int customerId,
    CancellationToken cancellationToken)
    {
        return await _dbContext.Customers
            .AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new CustomerSummary(
                c.Id,
                c.FirstName,
                c.LastName))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Customer with id {customerId} was not found.");
    }

    private async Task<Dictionary<int, Product>> GetProductsByIdAsync(
    IReadOnlyCollection<int> productIds,
    CancellationToken cancellationToken)
    {
        return await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }

    private static void ValidateProductsExist(
    IEnumerable<int> productIds,
    IReadOnlyDictionary<int, Product> products)
    {
        var missingProductIds = productIds
            .Where(id => !products.ContainsKey(id))
            .ToList();

        if (missingProductIds.Count > 0)
        {
            throw new NotFoundException(
                $"Products were not found: {string.Join(", ", missingProductIds)}.");
        }
    }

    private static void ValidateStockAvailability(
    IEnumerable<CreateOrderItemRequest> items,
    IReadOnlyDictionary<int, Product> products)
    {
        var insufficientStockItems = items
            .Where(item => products[item.ProductId].StockQuantity < item.Quantity)
            .Select(item => new
            {
                item.ProductId,
                ProductName = products[item.ProductId].Name,
                RequestedQuantity = item.Quantity,
                AvailableQuantity = products[item.ProductId].StockQuantity
            })
            .ToList();

        if (insufficientStockItems.Count == 0)
        {
            return;
        }

        var details = string.Join(
            "; ",
            insufficientStockItems.Select(item =>
                $"{item.ProductName} (ProductId: {item.ProductId}) requested: {item.RequestedQuantity}, available: {item.AvailableQuantity}"));

        throw new BadRequestException($"Insufficient stock. {details}");
    }

    private void AddAuditLog(
    string entityName,
    string entityId,
    string action,
    object? oldValues,
    object? newValues,
    DateTime createdAtUtc)
    {
        var auditLog = new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            CreatedAtUtc = createdAtUtc
        };

        _dbContext.AuditLogs.Add(auditLog);
    }
}