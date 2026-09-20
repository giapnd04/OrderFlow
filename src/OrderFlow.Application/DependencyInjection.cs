using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Features.Customers.CreateCustomer;
using OrderFlow.Application.Features.Orders.CancelOrder;
using OrderFlow.Application.Features.Orders.ConfirmOrder;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Application.Features.Orders.DeliverOrder;
using OrderFlow.Application.Features.Orders.GetOrderById;
using OrderFlow.Application.Features.Orders.GetOrders;
using OrderFlow.Application.Features.Orders.ShipOrder;
using OrderFlow.Application.Features.Payments.ProcessPayment;
using OrderFlow.Application.Features.Products.CreateProduct;
using OrderFlow.Application.Features.Products.GetProductById;

namespace OrderFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateOrderCommandHandler>();
        services.AddScoped<GetOrderByIdQueryHandler>();
        services.AddScoped<CancelOrderCommandHandler>();
        services.AddScoped<ConfirmOrderCommandHandler>();
        services.AddScoped<ShipOrderCommandHandler>();
        services.AddScoped<DeliverOrderCommandHandler>();
        services.AddScoped<ProcessPaymentCommandHandler>();
        services.AddScoped<GetOrdersQueryHandler>();
        services.AddScoped<CreateCustomerCommandHandler>();
        services.AddScoped<CreateProductCommandHandler>();
        services.AddScoped<GetProductByIdQueryHandler>();

        return services;
    }
}
