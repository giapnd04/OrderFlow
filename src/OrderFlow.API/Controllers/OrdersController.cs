using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Models.Orders;
using OrderFlow.Application.Features.Orders.CancelOrder;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Application.Features.Orders.GetOrderById;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CreateOrderCommandHandler _createOrder;
    private readonly GetOrderByIdQueryHandler _getOrderById;
    private readonly CancelOrderCommandHandler _cancelOrder;

    public OrdersController(
        CreateOrderCommandHandler createOrder, 
        GetOrderByIdQueryHandler getOrderById,
        CancelOrderCommandHandler cancelOrder)
    {
        _createOrder = createOrder;
        _getOrderById = getOrderById;
        _cancelOrder = cancelOrder;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateOrderResult), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateOrderResult>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand(
            request.CustomerId,
            request.Items
                .Select(item => new CreateOrderItemInput(item.ProductId, item.Quantity))
                .ToList());

        var result = await _createOrder.Handle(command, cancellationToken);

        return Created($"api/orders/{result.OrderId}", result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GetOrderByIdResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetOrderByIdResult>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var query = new GetOrderByIdQuery(id);

        var result = await _getOrderById.Handle(query, cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(
    int id,
    CancellationToken cancellationToken)
    {
        var command = new CancelOrderCommand(id);

        await _cancelOrder.Handle(
            command,
            cancellationToken);

        return NoContent();
    }
}
