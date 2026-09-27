using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Orders.GetOrderStatusSummary;

public sealed class GetOrderStatusSummaryQueryHandler
    : IQueryHandler<GetOrderStatusSummaryQuery, GetOrderStatusSummaryResult>
{
    private readonly IOrderReportRepository _reports;

    public GetOrderStatusSummaryQueryHandler(IOrderReportRepository reports)
    {
        _reports = reports;
    }

    public async Task<GetOrderStatusSummaryResult> Handle(
        GetOrderStatusSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var totals = (await _reports.GetStatusTotalsAsync(cancellationToken))
            .ToDictionary(t => t.Status);

        var items = Enum.GetValues<OrderStatus>()
            .Select(status => totals.TryGetValue(status, out var t)
                ? new GetOrderStatusSummaryItemResult(status.ToString(), t.OrderCount, t.TotalAmount)
                : new GetOrderStatusSummaryItemResult(status.ToString(), 0, 0m))
            .ToList();

        return new GetOrderStatusSummaryResult(
            items,
            items.Sum(i => i.OrderCount),
            items.Sum(i => i.TotalAmount));
    }
}
