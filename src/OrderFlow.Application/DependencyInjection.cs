using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Features.Orders.CancelOrder;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Application.Features.Orders.GetOrderById;
using OrderFlow.Application.Features.Payments.ProcessPayment;

namespace OrderFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateOrderCommandHandler>();
        services.AddScoped<GetOrderByIdQueryHandler>();
        services.AddScoped<CancelOrderCommandHandler>();
        services.AddScoped<ProcessPaymentCommandHandler>();

        return services;
    }
}
