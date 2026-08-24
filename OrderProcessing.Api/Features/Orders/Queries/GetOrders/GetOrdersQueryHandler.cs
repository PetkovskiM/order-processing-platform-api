using MediatR;
using OrderProcessing.Api.DTOs.Common;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Exceptions;
using OrderProcessing.Api.Features.Orders.Queries.ReadModel;
using OrderProcessing.ReadModels.Orders;

namespace OrderProcessing.Api.Features.Orders.Queries.GetOrders;

public sealed class GetOrdersQueryHandler
    : IRequestHandler<GetOrdersQuery, PagedResponse<OrderResponse>>
{
    private readonly IOrderReadModelReader _readModelReader;

    public GetOrdersQueryHandler(IOrderReadModelReader readModelReader)
    {
        _readModelReader = readModelReader;
    }

    public async Task<PagedResponse<OrderResponse>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var parameters = request.Parameters;

        ValidateDateRange(parameters);

        var page = await _readModelReader.GetPageAsync(parameters, cancellationToken);

        var orders = page.Items
            .Select(MapToResponse)
            .ToList();

        return new PagedResponse<OrderResponse>
        {
            Items = orders,
            Page = parameters.Page,
            PageSize = parameters.PageSize,
            TotalCount = page.TotalCount
        };
    }

    private static OrderResponse MapToResponse(OrderReadModel order)
    {
        return new OrderResponse
        {
            Id = order.OrderId,
            CustomerId = order.CustomerId,
            CustomerName = order.CustomerName,
            Status = Enum.Parse<OrderStatus>(
                order.Status,
                ignoreCase: true),
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

    private static void ValidateDateRange(OrderQueryParameters parameters)
    {
        if (parameters.CreatedFromUtc.HasValue &&
            parameters.CreatedToUtc.HasValue &&
            parameters.CreatedFromUtc > parameters.CreatedToUtc)
        {
            throw new BadRequestException(
                "CreatedFromUtc cannot be later than CreatedToUtc.",
                ErrorCodes.InvalidDateRange);
        }
    }
}