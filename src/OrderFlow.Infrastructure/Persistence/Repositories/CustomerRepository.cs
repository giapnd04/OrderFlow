using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly OrderFlowDbContext _db;

    public CustomerRepository(OrderFlowDbContext db) => _db = db;

    public Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default)
        => _db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken);

    public Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
        => _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public Task<Customer?> GetByIdForUpdateAsync(int customerId, CancellationToken cancellationToken = default)
        => _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public async Task<(IReadOnlyCollection<Customer> Customers, int TotalCount)> GetPagedAsync(
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                EF.Functions.Like(c.Name, $"%{search}%") ||
                EF.Functions.Like(c.Email, $"%{search}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var customers = await query
            .OrderBy(c => c.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (customers, totalCount);
    }

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Email == email, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    => _db.Customers.AnyAsync(c => c.Email == email, cancellationToken);

    public async Task<bool> AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _db.Customers.AddAsync(customer, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
