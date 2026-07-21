using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.DTOs.Common;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Exceptions;
using OrderProcessing.Api.Services.Auditing;
using OrderProcessing.Api.Services.Emailing;
using System.Net.Mail;
using System.Text.Json;

namespace OrderProcessing.Api.Services.Orders;

public class OrderService : IOrderService
{
    private readonly OrderProcessingDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrderProcessingDbContext dbContext, ILogger<OrderService> logger, IAuditService auditService, IEmailQueue emailQueue)
    {
        _dbContext = dbContext;
        _logger = logger;
        _auditService = auditService;
        _emailQueue = emailQueue;
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

            _auditService.Add(
            entityName: nameof(Order),
            entityId: order.Id.ToString(),
            action: AuditActions.Created,
            oldValues: null,
            newValues: new
            {
                order.Id,
                order.CustomerId,
                order.Status,
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
            createdAtUtc: utcNow);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
             "Order {OrderId} created for customer {CustomerId} with total amount {TotalAmount}",
             order.Id,
             order.CustomerId,
             order.TotalAmount);

            var emailMessage = CreateOrderCreatedEmail(order, customer);

            await TryEnqueueEmailAsync(
                emailMessage,
                order.Id,
                "Order Created",
                cancellationToken);

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

    public async Task<PagedResponse<OrderResponse>> GetAllAsync(
    OrderQueryParameters parameters,
    CancellationToken cancellationToken = default)
    {
        if (parameters.CreatedFromUtc.HasValue &&
            parameters.CreatedToUtc.HasValue &&
            parameters.CreatedFromUtc > parameters.CreatedToUtc)
        {
            throw new BadRequestException(
                "CreatedFromUtc cannot be later than CreatedToUtc.",
                ErrorCodes.InvalidDateRange);
        }

        var query = _dbContext.Orders
            .AsNoTracking()
            .AsQueryable();

        if (parameters.CustomerId.HasValue)
        {
            query = query.Where(
                order => order.CustomerId == parameters.CustomerId.Value);
        }

        if (parameters.Status.HasValue)
        {
            query = query.Where(
                order => order.Status == parameters.Status.Value);
        }

        if (parameters.CreatedFromUtc.HasValue)
        {
            query = query.Where(
                order => order.CreatedAtUtc >= parameters.CreatedFromUtc.Value);
        }

        if (parameters.CreatedToUtc.HasValue)
        {
            query = query.Where(
                order => order.CreatedAtUtc <= parameters.CreatedToUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orderedQuery = ApplySorting(
            query,
            parameters.SortBy,
            parameters.SortDirection);

        var orders = await orderedQuery
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(order => new OrderResponse
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                CustomerName =
                    order.Customer.FirstName + " " + order.Customer.LastName,
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
            .ToListAsync(cancellationToken);

        return new PagedResponse<OrderResponse>
        {
            Items = orders,
            Page = parameters.Page,
            PageSize = parameters.PageSize,
            TotalCount = totalCount
        };
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
            emailType: "Order-completed",
            cancellationToken);

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
     $"Only pending orders can be completed. Current status: {order.Status}.",
            ErrorCodes.InvalidOrderStatus);
        }

        var productIds = order.Items
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

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
            if (products.TryGetValue(item.ProductId, out var product))
            {
                product.StockQuantity += item.Quantity;
                product.UpdatedAtUtc = utcNow;
            }
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
            order.CancelledAtUtc,
            RestoredStock = order.Items.Select(item => new
            {
                item.ProductId,
                item.ProductName,
                item.Quantity
            })
        },
        createdAtUtc: utcNow);

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
     $"Duplicate products are not allowed. Product ids: {string.Join(", ", duplicateProductIds)}.",
            ErrorCodes.DuplicateOrderProducts);
        }
    }

    private sealed record CustomerSummary(
    int Id,
    string FirstName,
    string LastName,
    string Email
    );

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
                c.LastName,
                c.Email))
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

        throw new BadRequestException(
  $"Insufficient stock. {details}",
         ErrorCodes.InsufficientStock);
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

    private static IOrderedQueryable<Order> ApplySorting(
    IQueryable<Order> query,
    OrderSortBy sortBy,
    SortDirection sortDirection)
    {
        return (sortBy, sortDirection) switch
        {
            (OrderSortBy.Id, SortDirection.Ascending) =>
                query.OrderBy(order => order.Id),

            (OrderSortBy.Id, SortDirection.Descending) =>
                query.OrderByDescending(order => order.Id),

            (OrderSortBy.TotalAmount, SortDirection.Ascending) =>
                query
                    .OrderBy(order => order.TotalAmount)
                    .ThenBy(order => order.Id),

            (OrderSortBy.TotalAmount, SortDirection.Descending) =>
                query
                    .OrderByDescending(order => order.TotalAmount)
                    .ThenByDescending(order => order.Id),

            (OrderSortBy.CreatedAtUtc, SortDirection.Ascending) =>
                query
                    .OrderBy(order => order.CreatedAtUtc)
                    .ThenBy(order => order.Id),

            _ =>
                query
                    .OrderByDescending(order => order.CreatedAtUtc)
                    .ThenByDescending(order => order.Id)
        };
    }

    private static EmailMessage CreateOrderCreatedEmail(
    Order order,
    CustomerSummary customer)
    {
        var customerName = $"{customer.FirstName} {customer.LastName}";

        var itemLines = order.Items.Select(item =>
            $"- {item.ProductName}: {item.Quantity} × {item.UnitPrice:F2} = {item.LineTotal:F2}");

        var body = $"""
        Hello {customerName},

        Your order #{order.Id} has been created successfully.

        Items:
        {string.Join(Environment.NewLine, itemLines)}

        Total amount: {order.TotalAmount:F2}
        Status: {order.Status}

        Thank you.
        """;

        return new EmailMessage
        {
            To = customer.Email,
            Subject = $"Order #{order.Id} created",
            Body = body,
            IsHtml = false
        };
    }

    private async Task TryEnqueueEmailAsync(
     EmailMessage message,
     int orderId,
     string emailType,
     CancellationToken cancellationToken)
    {
        try
        {
            await _emailQueue.EnqueueAsync(message, cancellationToken);

            _logger.LogInformation(
                "{EmailType} email queued. OrderId: {OrderId}, Recipient: {Recipient}",
                emailType,
                orderId,
                message.To);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Order {OrderId} was saved, but enqueueing the {EmailType} email was cancelled",
                orderId,
                emailType);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Order {OrderId} was saved, but its {EmailType} email could not be queued",
                orderId,
                emailType);
        }
    }

    private static EmailMessage CreateOrderCompletedEmail(Order order)
    {
        var customerName =
            $"{order.Customer.FirstName} {order.Customer.LastName}";

        var itemLines = order.Items.Select(item =>
            $"- {item.ProductName}: {item.Quantity} × {item.UnitPrice:F2} = {item.LineTotal:F2}");

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
}