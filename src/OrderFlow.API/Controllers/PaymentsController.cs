using Microsoft.AspNetCore.Mvc;
using OrderFlow.Application.Features.Payments.GetPaymentById;
using OrderFlow.Application.Features.Payments.GetPayments;
using OrderFlow.Domain.Enums;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly GetPaymentByIdQueryHandler _getPaymentById;
    private readonly GetPaymentsQueryHandler _getPayments;

    public PaymentsController(
        GetPaymentByIdQueryHandler getPaymentById,
        GetPaymentsQueryHandler getPayments)
    {
        _getPaymentById = getPaymentById;
        _getPayments = getPayments;
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GetPaymentByIdResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetPaymentByIdResult>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _getPaymentById.Handle(new GetPaymentByIdQuery(id), cancellationToken);

        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetPaymentsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetPaymentsResult>> GetPayments(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] PaymentAttemptStatus? status = null,
        [FromQuery] string? provider = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPaymentsQuery(pageNumber, pageSize, status, provider);

        var result = await _getPayments.Handle(query, cancellationToken);

        return Ok(result);
    }
}
