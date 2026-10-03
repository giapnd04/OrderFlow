using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

internal sealed class EmailVerificationOtpRepository : IEmailVerificationOtpRepository
{
    private readonly OrderFlowDbContext _db;

    public EmailVerificationOtpRepository(OrderFlowDbContext db) => _db = db;

    public async Task AddAsync(EmailVerificationOtp otp, CancellationToken cancellationToken = default)
    {
        _db.EmailVerificationOtps.Add(otp);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<EmailVerificationOtp?> GetLatestUsableForUpdateAsync(
        int userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
        => _db.EmailVerificationOtps
            .Where(o => o.UserId == userId && o.ConsumedAt == null && o.ExpiresAt > nowUtc)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task UpdateAsync(EmailVerificationOtp otp, CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}
