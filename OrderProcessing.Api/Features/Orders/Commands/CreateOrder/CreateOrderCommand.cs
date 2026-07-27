using MediatR;
using OrderProcessing.Api.DTOs.Orders;

namespace OrderProcessing.Api.Features.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(CreateOrderRequest Request) : IRequest<OrderResponse>;