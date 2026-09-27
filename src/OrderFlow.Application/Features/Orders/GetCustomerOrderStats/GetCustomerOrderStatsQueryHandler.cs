using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Orders.GetCustomerOrderStats;

public sealed class GetCustomerOrderStatsQueryHandler
    : IQueryHandler<GetCustomerOrderStatsQuery, GetCustomerOrderStatsResult>
{
    private readonly ICustomerRepository _customers;
    private readonly IOrderReportRepository _reports;

    public GetCustomerOrderStatsQueryHandler(ICustomerRepository customers, IOrderReportRepository reports)
    {
        _customers = customers;
        _reports = reports;
    }

    public async Task<GetCustomerOrderStatsResult> Handle(
        GetCustomerOrderStatsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.CustomerId <= 0)
        {
            throw new ValidationException("CustomerId must be greater than zero.");
        }

        if (!await _customers.ExistsAsync(query.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer", query.CustomerId);
        }

        var totals = await _reports.GetCustomerTotalsAsync(query.CustomerId, cancellationToken);

        return new GetCustomerOrderStatsResult(
            query.CustomerId,
            totals.OrderCount,
            totals.SpentAmount,
            totals.LastOrderAt);
    }
}
