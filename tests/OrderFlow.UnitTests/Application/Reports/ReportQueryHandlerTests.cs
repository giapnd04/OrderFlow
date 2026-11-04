using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Reports;
using OrderFlow.UnitTests.Application.Auth;

namespace OrderFlow.UnitTests.Application.Reports;

public sealed class ReportQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeReportRepository _reports = new();

    [Fact]
    public async Task MonthlyRevenue_FromNotBeforeTo_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() => new GetMonthlyRevenueQueryHandler(_reports)
            .Handle(new GetMonthlyRevenueQuery(Now, Now)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task TopCustomers_TopOutOfRange_ThrowsValidation(int top)
    {
        await Assert.ThrowsAsync<ValidationException>(() => new GetTopCustomersQueryHandler(_reports)
            .Handle(new GetTopCustomersQuery(top)));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(24, 0)]
    [InlineData(24, 501)]
    public async Task StalePendingOrders_BadParameters_ThrowValidation(int hours, int limit)
    {
        await Assert.ThrowsAsync<ValidationException>(() => StaleHandler()
            .Handle(new GetStalePendingOrdersQuery(hours, limit)));
    }

    [Fact]
    public async Task StalePendingOrders_CutoffIsNowMinusTheHours()
    {
        await StaleHandler().Handle(new GetStalePendingOrdersQuery(24, 10));

        Assert.Equal(Now.AddHours(-24), _reports.LastCutoff);
    }

    private GetStalePendingOrdersQueryHandler StaleHandler() => new(_reports, new FixedTimeProvider(Now));

    private sealed class FakeReportRepository : IReportRepository
    {
        public DateTime? LastCutoff { get; private set; }

        public Task<IReadOnlyList<MonthlyRevenueRow>> GetMonthlyRevenueAsync(
            DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MonthlyRevenueRow>>([]);

        public Task<IReadOnlyList<TopCustomerRow>> GetTopCustomersAsync(int top, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopCustomerRow>>([]);

        public Task<IReadOnlyList<StalePendingOrderRow>> GetStalePendingOrdersAsync(
            DateTime cutoffUtc, int limit, CancellationToken cancellationToken = default)
        {
            LastCutoff = cutoffUtc;
            return Task.FromResult<IReadOnlyList<StalePendingOrderRow>>([]);
        }
    }
}
