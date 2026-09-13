using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Models.Orders;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Features.Orders.CancelOrder;
using OrderFlow.Application.Features.Orders.ConfirmOrder;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Application.Features.Orders.GetOrderById;
using OrderFlow.Application.Features.Payments.ProcessPayment;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CreateOrderCommandHandler _createOrder;
    private readonly GetOrderByIdQueryHandler _getOrderById;
    private readonly CancelOrderCommandHandler _cancelOrder;
    private readonly ConfirmOrderCommandHandler _confirmOrder;
    private readonly ProcessPaymentCommandHandler _processPayment;

    public OrdersController(
        CreateOrderCommandHandler createOrder,
        GetOrderByIdQueryHandler getOrderById,
        CancelOrderCommandHandler cancelOrder,
        ConfirmOrderCommandHandler confirmOrder,
        ProcessPaymentCommandHandler processPayment)
    {
        _createOrder = createOrder;
        _getOrderById = getOrderById;
        _cancelOrder = cancelOrder;
        _confirmOrder = confirmOrder;
        _processPayment = processPayment;
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
}
