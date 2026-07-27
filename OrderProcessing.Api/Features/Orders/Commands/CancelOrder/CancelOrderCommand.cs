using MediatR;
using OrderProcessing.Api.DTOs.Orders;

namespace OrderProcessing.Api.Features.Orders.Commands.CancelOrder;

public sealed record CancelOrderCommand(int OrderId) : IRequest<OrderResponse>;