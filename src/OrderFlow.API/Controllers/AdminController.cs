using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Authorization;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Features.Payments.ReconcilePayments;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly ICommandHandler<ReconcilePaymentsCommand, ReconcilePaymentsResult> _reconcilePayments;

    public AdminController(ICommandHandler<ReconcilePaymentsCommand, ReconcilePaymentsResult> reconcilePayments)
    {
        _reconcilePayments = reconcilePayments;
    }

    [HttpPost("reconciliation/payments")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(typeof(ReconcilePaymentsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReconcilePaymentsResult>> ReconcilePayments(CancellationToken cancellationToken)
    {
        var result = await _reconcilePayments.Handle(new ReconcilePaymentsCommand(), cancellationToken);

        return Ok(result);
    }
}
