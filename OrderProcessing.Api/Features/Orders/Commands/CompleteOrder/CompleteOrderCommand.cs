using MediatR;
using OrderProcessing.Api.DTOs.Orders;

namespace OrderProcessing.Api.Features.Orders.Commands.CompleteOrder;

public sealed record CompleteOrderCommand(int OrderId) : IRequest<OrderResponse>;