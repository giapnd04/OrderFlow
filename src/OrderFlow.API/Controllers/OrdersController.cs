using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Models.Orders;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Features.Orders.CancelOrder;
using OrderFlow.Application.Features.Orders.ConfirmOrder;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Application.Features.Orders.DeliverOrder;
using OrderFlow.Application.Features.Orders.GetOrderById;
using OrderFlow.Application.Features.Orders.GetOrders;
using OrderFlow.Application.Features.Orders.ShipOrder;
using OrderFlow.Application.Features.Payments.ProcessPayment;
using OrderFlow.Domain.Enums;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CreateOrderCommandHandler _createOrder;
    private readonly GetOrderByIdQueryHandler _getOrderById;
    private readonly CancelOrderCommandHandler _cancelOrder;
    private readonly ConfirmOrderCommandHandler _confirmOrder;
    private readonly ShipOrderCommandHandler _shipOrder;
    private readonly DeliverOrderCommandHandler _deliverOrder;
    private readonly ProcessPaymentCommandHandler _processPayment;
    private readonly GetOrdersQueryHandler _getOrders;

    public OrdersController(
        CreateOrderCommandHandler createOrder,
        GetOrderByIdQueryHandler getOrderById,
        CancelOrderCommandHandler cancelOrder,
        ConfirmOrderCommandHandler confirmOrder,
        ShipOrderCommandHandler shipOrder,
        DeliverOrderCommandHandler deliverOrder,
        ProcessPaymentCommandHandler processPayment,
        GetOrdersQueryHandler getOrders)
    {
        _createOrder = createOrder;
        _getOrderById = getOrderById;
        _cancelOrder = cancelOrder;
        _confirmOrder = confirmOrder;
        _shipOrder = shipOrder;
        _deliverOrder = deliverOrder;
        _processPayment = processPayment;
        _getOrders = getOrders;
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

    [HttpPost("{id:int}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Confirm(
    int id,
    CancellationToken cancellationToken)
    {
        var command = new ConfirmOrderCommand(id);

        await _confirmOrder.Handle(
            command,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:int}/ship")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Ship(
    int id,
    CancellationToken cancellationToken)
    {
        var command = new ShipOrderCommand(id);

        await _shipOrder.Handle(
            command,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:int}/deliver")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deliver(
    int id,
    CancellationToken cancellationToken)
    {
        var command = new DeliverOrderCommand(id);

        await _deliverOrder.Handle(
            command,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:int}/payments")]
    public async Task<IActionResult> ProcessPayment(
    int id,
    [FromBody] ProcessPaymentRequest request,
    CancellationToken cancellationToken)
    {
        var command = new ProcessPaymentCommand(
            id,
            request.Amount,
            request.Provider,
            request.ProviderTransactionId,
            request.Status);

        var result = await _processPayment.Handle(
            command,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetOrdersResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetOrdersResult>> GetOrders(
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] OrderStatus? status = null,
    [FromQuery] int? customerId = null,
    CancellationToken cancellationToken = default)
    {
        var query = new GetOrdersQuery(
            pageNumber,
            pageSize,
            status,
            customerId);

        var result = await _getOrders.Handle(
            query,
            cancellationToken);

        return Ok(result);
    }
}
