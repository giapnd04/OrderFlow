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
        await _db.SaveChangesAsync(cancellationToken); 
    }

    public async Task<IReadOnlyList<PaymentAttempt>> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        return await _db.PaymentAttempts
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaymentAttempt?> GetByIdAsync(
        int paymentAttemptId,
        CancellationToken cancellationToken = default)
    {
        return await _db.PaymentAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentAttemptId, cancellationToken);
    }

    public async Task<(IReadOnlyCollection<PaymentAttempt> Payments, int TotalCount)> GetPagedAsync(
        PaymentAttemptStatus? status,
        string? provider,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.PaymentAttempts.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(x => x.Provider == provider);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var payments = await query
            .OrderByDescending(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (payments, totalCount);
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