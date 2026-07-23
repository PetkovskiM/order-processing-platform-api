using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Api.DTOs.Common;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Services.Orders;
using MediatR;
using OrderProcessing.Api.Features.Orders.Commands.CompleteOrder;
using OrderProcessing.Api.Features.Orders.Queries.GetOrderById;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ISender _sender;

    public OrdersController(IOrderService orderService, ISender sender)
    {
        _orderService = orderService;
        _sender = sender;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            order);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<OrderResponse>>> GetAll(
     [FromQuery] OrderQueryParameters parameters,
     CancellationToken cancellationToken)
    {
        var orders = await _orderService.GetAllAsync(
            parameters,
            cancellationToken);

        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await _sender.Send(new GetOrderByIdQuery(id), cancellationToken);

        return Ok(order);
    }

    [HttpPatch("{id:int}/complete")]
    public async Task<ActionResult<OrderResponse>> Complete(
    int id,
    CancellationToken cancellationToken)
    {
        var order = await _sender.Send(new CompleteOrderCommand(id), cancellationToken);

        return Ok(order);
    }

    [HttpPatch("{id:int}/cancel")]
    public async Task<ActionResult<OrderResponse>> Cancel(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.CancelAsync(id, cancellationToken);

        return Ok(order);
    }
}