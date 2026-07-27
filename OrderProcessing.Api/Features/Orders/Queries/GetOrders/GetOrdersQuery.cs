using MediatR;
using OrderProcessing.Api.DTOs.Common;
using OrderProcessing.Api.DTOs.Orders;

namespace OrderProcessing.Api.Features.Orders.Queries.GetOrders;

public sealed record GetOrdersQuery(OrderQueryParameters Parameters) : IRequest<PagedResponse<OrderResponse>>;