using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Reports;

public sealed record GetMonthlyRevenueQuery(DateTime From, DateTime To) : IQuery<IReadOnlyList<MonthlyRevenueRow>>;

public sealed class GetMonthlyRevenueQueryHandler
    : IQueryHandler<GetMonthlyRevenueQuery, IReadOnlyList<MonthlyRevenueRow>>
{
    private readonly IReportRepository _reports;

    public GetMonthlyRevenueQueryHandler(IReportRepository reports) => _reports = reports;

    public Task<IReadOnlyList<MonthlyRevenueRow>> Handle(
        GetMonthlyRevenueQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.From >= query.To)
        {
            throw new ValidationException("'from' must be earlier than 'to'.");
        }

        return _reports.GetMonthlyRevenueAsync(query.From, query.To, cancellationToken);
    }
}

public sealed record GetTopCustomersQuery(int Top) : IQuery<IReadOnlyList<TopCustomerRow>>;

public sealed class GetTopCustomersQueryHandler
    : IQueryHandler<GetTopCustomersQuery, IReadOnlyList<TopCustomerRow>>
{
    private const int MaxTop = 100;

    private readonly IReportRepository _reports;

    public GetTopCustomersQueryHandler(IReportRepository reports) => _reports = reports;

    public Task<IReadOnlyList<TopCustomerRow>> Handle(
        GetTopCustomersQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Top is <= 0 or > MaxTop)
        {
            throw new ValidationException($"Top must be between 1 and {MaxTop}.");
        }

        return _reports.GetTopCustomersAsync(query.Top, cancellationToken);
    }
}

public sealed record GetStalePendingOrdersQuery(int OlderThanHours, int Limit) : IQuery<IReadOnlyList<StalePendingOrderRow>>;

public sealed class GetStalePendingOrdersQueryHandler
    : IQueryHandler<GetStalePendingOrdersQuery, IReadOnlyList<StalePendingOrderRow>>
{
    private const int MaxLimit = 500;

    private readonly IReportRepository _reports;
    private readonly TimeProvider _clock;

    public GetStalePendingOrdersQueryHandler(IReportRepository reports, TimeProvider clock)
    {
        _reports = reports;
        _clock = clock;
    }

    public Task<IReadOnlyList<StalePendingOrderRow>> Handle(
        GetStalePendingOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.OlderThanHours <= 0)
        {
            throw new ValidationException("OlderThanHours must be greater than zero.");
        }

        if (query.Limit is <= 0 or > MaxLimit)
        {
            throw new ValidationException($"Limit must be between 1 and {MaxLimit}.");
        }

        var cutoff = _clock.GetUtcNow().UtcDateTime.AddHours(-query.OlderThanHours);

        return _reports.GetStalePendingOrdersAsync(cutoff, query.Limit, cancellationToken);
    }
}
