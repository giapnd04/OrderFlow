using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderFlow.Application.Abstractions.Notifications;
using OrderFlow.Application.Abstractions.Payments;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Infrastructure.Authentication;
using OrderFlow.Infrastructure.ExternalServices;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Infrastructure.Persistence.Repositories;
using OrderFlow.Infrastructure.Persistence.Simulation;

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

        // A query that hangs must fail rather than hold a connection and a request forever (OF-10).
        var commandTimeoutSeconds = int.TryParse(configuration["Database:CommandTimeoutSeconds"], out var seconds) && seconds > 0
            ? seconds
            : 30;

        services.AddDbContext<OrderFlowDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql
                    .MigrationsAssembly(typeof(OrderFlowDbContext).Assembly.FullName)
                    .CommandTimeout(commandTimeoutSeconds)));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductReportRepository, ProductReportRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderQueries, OrderQueries>();
        services.AddScoped<IOrderReportRepository, OrderReportRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEmailVerificationOtpRepository, EmailVerificationOtpRepository>();
        services.AddScoped<ITransactionRunner, EfTransactionRunner>();
        services.AddScoped<ILoginThrottleStore, SqlLoginThrottleStore>();
        services.AddScoped<IPaymentProviderLedger, SimPaymentProviderLedger>();

        // Fails fast at startup if the signing key is missing or too short.
        var jwtOptions = JwtOptions.FromConfiguration(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(jwtOptions);
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IOtpGenerator, SecureOtpGenerator>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();

        return services;
    }
}
