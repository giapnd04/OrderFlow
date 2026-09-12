using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Infrastructure.Persistence.Repositories;

namespace OrderFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrderFlow")
            ?? throw new InvalidOperationException(
                "Missing required connection string 'OrderFlow' (ConnectionStrings:OrderFlow).");

        services.AddDbContext<OrderFlowDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsAssembly(typeof(OrderFlowDbContext).Assembly.FullName)));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        return services;
    }
}
