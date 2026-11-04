using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Authorization;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Features.Reports;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly GetMonthlyRevenueQueryHandler _monthlyRevenue;
    private readonly GetTopCustomersQueryHandler _topCustomers;
    private readonly GetStalePendingOrdersQueryHandler _stalePendingOrders;

    public ReportsController(
        GetMonthlyRevenueQueryHandler monthlyRevenue,
        GetTopCustomersQueryHandler topCustomers,
        GetStalePendingOrdersQueryHandler stalePendingOrders)
    {
        _monthlyRevenue = monthlyRevenue;
        _topCustomers = topCustomers;
        _stalePendingOrders = stalePendingOrders;
    }

    [HttpGet("monthly-revenue")]
    [Authorize(Policy = Policies.Sales)]
    [ProducesResponseType(typeof(IReadOnlyList<MonthlyRevenueRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MonthlyRevenueRow>>> GetMonthlyRevenue(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        return Ok(await _monthlyRevenue.Handle(new GetMonthlyRevenueQuery(from, to), cancellationToken));
    }

    [HttpGet("top-customers")]
    [Authorize(Policy = Policies.Sales)]
    [ProducesResponseType(typeof(IReadOnlyList<TopCustomerRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TopCustomerRow>>> GetTopCustomers(
        [FromQuery] int top = 10,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _topCustomers.Handle(new GetTopCustomersQuery(top), cancellationToken));
    }

    [HttpGet("stale-pending-orders")]
    [Authorize(Policy = Policies.Sales)]
    [ProducesResponseType(typeof(IReadOnlyList<StalePendingOrderRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StalePendingOrderRow>>> GetStalePendingOrders(
        [FromQuery] int olderThanHours = 24,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _stalePendingOrders.Handle(new GetStalePendingOrdersQuery(olderThanHours, limit), cancellationToken));
    }
}
