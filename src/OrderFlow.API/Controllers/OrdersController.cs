using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Authorization;
using OrderFlow.API.Models.Orders;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Orders.CancelOrder;
using OrderFlow.Application.Features.Orders.ConfirmOrder;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Application.Features.Orders.DeliverOrder;
using OrderFlow.Application.Features.Orders.GetOrderById;
using OrderFlow.Application.Features.Orders.GetOrderStatusSummary;
using OrderFlow.Application.Features.Orders.GetOrders;
using OrderFlow.Application.Features.Orders.ShipOrder;
using OrderFlow.Application.Features.Payments.GetOrderPayments;
using OrderFlow.Application.Features.Payments.GetOrderPaymentSummary;
using OrderFlow.Application.Features.Payments.ProcessPayment;
using OrderFlow.Domain.Enums;

namespace OrderFlow.API.Controllers;

/// <summary>
/// Orders are the one resource customers touch directly (ADR-002). The policy attribute decides
/// which roles may call an action at all; for the Customer role the actions that take an order
/// additionally check ownership here (<c>currentUser.CustomerId</c> vs the order's customer).
/// Staff roles skip the ownership check. A customer asking for someone else's order gets a 404,
/// never a 403, so order ids can't be probed.
/// </summary>
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
    private readonly GetOrderPaymentsQueryHandler _getOrderPayments;
    private readonly GetOrderPaymentSummaryQueryHandler _getOrderPaymentSummary;
    private readonly GetOrderStatusSummaryQueryHandler _getOrderStatusSummary;
    private readonly ICurrentUser _currentUser;

    public OrdersController(
        CreateOrderCommandHandler createOrder,
        GetOrderByIdQueryHandler getOrderById,
        CancelOrderCommandHandler cancelOrder,
        ConfirmOrderCommandHandler confirmOrder,
        ShipOrderCommandHandler shipOrder,
        DeliverOrderCommandHandler deliverOrder,
        ProcessPaymentCommandHandler processPayment,
        GetOrdersQueryHandler getOrders,
        GetOrderPaymentsQueryHandler getOrderPayments,
        GetOrderPaymentSummaryQueryHandler getOrderPaymentSummary,
        GetOrderStatusSummaryQueryHandler getOrderStatusSummary,
        ICurrentUser currentUser)
    {
        _createOrder = createOrder;
        _getOrderById = getOrderById;
        _cancelOrder = cancelOrder;
        _confirmOrder = confirmOrder;
        _shipOrder = shipOrder;
        _deliverOrder = deliverOrder;
        _processPayment = processPayment;
        _getOrders = getOrders;
        _getOrderPayments = getOrderPayments;
        _getOrderPaymentSummary = getOrderPaymentSummary;
        _getOrderStatusSummary = getOrderStatusSummary;
        _currentUser = currentUser;
    }

    private bool IsCustomer => _currentUser.Role == UserRole.Customer;

    /// <summary>The caller's own customer id; a customer whose email is still unverified (claim pending) has none yet.</summary>
    private int RequireOwnCustomerId()
        => _currentUser.CustomerId
           ?? throw new ForbiddenException(
               "Your account is not linked to a customer record yet. Verify your email first, then log in again.");

    [HttpPost]
    [Authorize(Policy = Policies.CustomerOrSales)]
    [ProducesResponseType(typeof(CreateOrderResult), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateOrderResult>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (IsCustomer && request.CustomerId != RequireOwnCustomerId())
        {
            throw new ForbiddenException("You can only create orders for your own customer record.");
        }

        var command = new CreateOrderCommand(
            request.CustomerId,
            request.Items
                .Select(item => new CreateOrderItemInput(item.ProductId, item.Quantity))
                .ToList());

        var result = await _createOrder.Handle(command, cancellationToken);

        return Created($"api/orders/{result.OrderId}", result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Authenticated)]
    [ProducesResponseType(typeof(GetOrderByIdResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetOrderByIdResult>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _getOrderById.Handle(new GetOrderByIdQuery(id), cancellationToken);

        if (IsCustomer && result.CustomerId != _currentUser.CustomerId)
        {
            throw new NotFoundException("Order", id);
        }

        return Ok(result);
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Policies.CustomerOrSales)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(
        int id,
        CancellationToken cancellationToken)
    {
        if (IsCustomer)
        {
            // Check ownership BEFORE mutating anything.
            var order = await _getOrderById.Handle(new GetOrderByIdQuery(id), cancellationToken);

            if (order.CustomerId != _currentUser.CustomerId)
            {
                throw new NotFoundException("Order", id);
            }
        }

        await _cancelOrder.Handle(new CancelOrderCommand(id), cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:int}/confirm")]
    [Authorize(Policy = Policies.Sales)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Confirm(
        int id,
        CancellationToken cancellationToken)
    {
        await _confirmOrder.Handle(new ConfirmOrderCommand(id), cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:int}/ship")]
    [Authorize(Policy = Policies.Warehouse)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Ship(
        int id,
        CancellationToken cancellationToken)
    {
        await _shipOrder.Handle(new ShipOrderCommand(id), cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:int}/deliver")]
    [Authorize(Policy = Policies.Warehouse)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deliver(
        int id,
        CancellationToken cancellationToken)
    {
        await _deliverOrder.Handle(new DeliverOrderCommand(id), cancellationToken);

        return NoContent();
    }

    [HttpGet("summary")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(GetOrderStatusSummaryResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetOrderStatusSummaryResult>> GetStatusSummary(
        CancellationToken cancellationToken)
    {
        var result = await _getOrderStatusSummary.Handle(new GetOrderStatusSummaryQuery(), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}/payments")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(GetOrderPaymentsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetOrderPaymentsResult>> GetPayments(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _getOrderPayments.Handle(new GetOrderPaymentsQuery(id), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}/payments/summary")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(GetOrderPaymentSummaryResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetOrderPaymentSummaryResult>> GetPaymentSummary(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _getOrderPaymentSummary.Handle(new GetOrderPaymentSummaryQuery(id), cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:int}/payments")]
    [Authorize(Policy = Policies.Sales)]
    [ProducesResponseType(typeof(ProcessPaymentResult), StatusCodes.Status200OK)]
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

        var result = await _processPayment.Handle(command, cancellationToken);

        return Ok(result);
    }

    [HttpGet]
    [Authorize(Policy = Policies.Authenticated)]
    [ProducesResponseType(typeof(GetOrdersResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetOrdersResult>> GetOrders(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] int? customerId = null,
        CancellationToken cancellationToken = default)
    {
        // A customer can only ever list their own orders, whatever filter they send.
        var effectiveCustomerId = IsCustomer ? RequireOwnCustomerId() : customerId;

        var result = await _getOrders.Handle(
            new GetOrdersQuery(pageNumber, pageSize, status, effectiveCustomerId),
            cancellationToken);

        return Ok(result);
    }
}
