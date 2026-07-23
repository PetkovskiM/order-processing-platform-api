using MediatR;
using OrderProcessing.Api.DTOs.Orders;

namespace OrderProcessing.Api.Features.Orders.Queries.GetOrderById;

public sealed record GetOrderByIdQuery(int OrderId) : IRequest<OrderResponse>;