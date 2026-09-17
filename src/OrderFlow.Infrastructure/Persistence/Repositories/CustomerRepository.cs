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


}
