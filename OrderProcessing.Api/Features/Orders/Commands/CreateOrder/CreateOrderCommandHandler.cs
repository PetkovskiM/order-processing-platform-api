using MediatR;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Services.Orders;

namespace OrderProcessing.Api.Features.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler
    : IRequestHandler<CreateOrderCommand, OrderResponse>
{
    private readonly IOrderService _orderService;

    public CreateOrderCommandHandler(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public Task<OrderResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        return _orderService.CreateAsync(request.Request, cancellationToken);
    }
}