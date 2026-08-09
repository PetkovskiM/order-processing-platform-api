using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Exceptions;
using OrderProcessing.Api.Services.Auditing;
using OrderProcessing.Api.Services.Outbox;
using OrderProcessing.Contracts.Orders;

namespace OrderProcessing.Api.Features.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderResponse>
{
    private readonly OrderProcessingDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreateOrderCommandHandler> _logger;
    private readonly IOutboxWriter _outboxWriter;

    public CreateOrderCommandHandler(
        OrderProcessingDbContext dbContext,
        IAuditService auditService,
        ILogger<CreateOrderCommandHandler> logger,
        IOutboxWriter outboxWriter)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
        _outboxWriter = outboxWriter;
    }

    public async Task<OrderResponse> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

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

            var integrationEvent = CreateOrderCreatedIntegrationEvent(order, customer);

            _outboxWriter.Add(integrationEvent);

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

    private sealed record CustomerSummary(
    int Id,
    string FirstName,
    string LastName,
    string Email
    );

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


    private static OrderCreatedIntegrationEvent CreateOrderCreatedIntegrationEvent(Order order, CustomerSummary customer)
    {
        return new OrderCreatedIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAtUtc: order.CreatedAtUtc,
            OrderId: order.Id,
            CustomerId: order.CustomerId,
            CustomerName:
                $"{customer.FirstName} {customer.LastName}",
            CustomerEmail: customer.Email,
            TotalAmount: order.TotalAmount,
            CreatedAtUtc: order.CreatedAtUtc,
            Items: order.Items
                .OrderBy(item => item.Id)
                .Select(item =>
                    new OrderItemIntegrationModel(
                        ProductId: item.ProductId,
                        ProductName: item.ProductName,
                        Quantity: item.Quantity,
                        UnitPrice: item.UnitPrice,
                        LineTotal: item.LineTotal))
                .ToArray());
    }
}