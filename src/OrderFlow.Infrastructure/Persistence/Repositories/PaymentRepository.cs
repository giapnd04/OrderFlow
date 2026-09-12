using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

internal sealed class PaymentRepository : IPaymentRepository
{
    private readonly OrderFlowDbContext _db;

    public PaymentRepository(OrderFlowDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(
        PaymentAttempt paymentAttempt,
        CancellationToken cancellationToken = default)
    {
        await _db.PaymentAttempts.AddAsync(paymentAttempt, cancellationToken);
    }

    public async Task<decimal> GetSucceededAmountAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        return await _db.PaymentAttempts
            .Where(x =>
                x.OrderId == orderId &&
                x.Status == PaymentAttemptStatus.Succeeded)
            .Select(x => (decimal?)x.Amount)
            .SumAsync(cancellationToken) ?? 0m;
    }
}