using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Models.Orders;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Application.Features.Orders.GetOrderById;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CreateOrderCommandHandler _createOrder;
    private readonly GetOrderByIdCommandHandler _getOrderByIdCommandHandler;

    public OrdersController(CreateOrderCommandHandler createOrder, GetOrderByIdCommandHandler getOrderByIdCommandHandler)
    {
        _createOrder = createOrder;
        _getOrderByIdCommandHandler = getOrderByIdCommandHandler;

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
public async Task<ActionResult<GetOrderByIdResult>> GetById(
    int id,
    CancellationToken cancellationToken)
{
    var query = new GetOrderByIdCommand(id);

    var result = await _getOrderByIdCommandHandler.Handle(
        query,
        cancellationToken);

    return Ok(result);
}
}
