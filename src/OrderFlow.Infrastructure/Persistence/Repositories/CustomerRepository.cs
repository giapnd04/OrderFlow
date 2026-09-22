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

    //public async Task<bool> ExistsAsync(
    //int customerId,
    //CancellationToken cancellationToken = default)
    //{
    //    var database = _db.Database.GetDbConnection().Database;
    //    var server = _db.Database.GetDbConnection().DataSource;

    //    Console.WriteLine($"[DB] Server: {server}");
    //    Console.WriteLine($"[DB] Database: {database}");
    //    Console.WriteLine($"[DB] CustomerId: {customerId}");

    //    var exists = await _db.Customers
    //        .AnyAsync(c => c.Id == customerId, cancellationToken);

    //    Console.WriteLine($"[DB] Customer exists: {exists}");

    //    return exists;
    //}

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
}
