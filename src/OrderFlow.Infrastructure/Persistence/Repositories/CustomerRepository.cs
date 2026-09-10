using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly OrderFlowDbContext _db;

    public CustomerRepository(OrderFlowDbContext db) => _db = db;

    public Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default)
        => _db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken);
}
