using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Application.Features.Orders.GetOrderById;

namespace OrderFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateOrderCommandHandler>();
        services.AddScoped<GetOrderByIdCommandHandler>();


        return services;
    }
}
