using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderFlow.Application.Behaviors;
using OrderFlow.Application.Features.Auth.ConfirmEmailOtp;
using OrderFlow.Application.Features.Auth.CreateStaffUser;
using OrderFlow.Application.Features.Auth.Login;
using OrderFlow.Application.Features.Auth.Register;
using OrderFlow.Application.Features.Customers.ChangeCustomerEmail;
using OrderFlow.Application.Features.Customers.CreateCustomer;
using OrderFlow.Application.Features.Customers.DeactivateCustomer;
using OrderFlow.Application.Features.Customers.DeleteCustomer;
using OrderFlow.Application.Features.Customers.GetCustomerById;
using OrderFlow.Application.Features.Customers.GetCustomers;
using OrderFlow.Application.Features.Customers.ReactivateCustomer;
using OrderFlow.Application.Features.Customers.UpdateCustomer;
using OrderFlow.Application.Features.Orders.CancelOrder;
using OrderFlow.Application.Features.Orders.ConfirmOrder;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Application.Features.Orders.DeliverOrder;
using OrderFlow.Application.Features.Orders.GetCustomerOrderStats;
using OrderFlow.Application.Features.Orders.GetOrderById;
using OrderFlow.Application.Features.Orders.GetOrderStatusSummary;
using OrderFlow.Application.Features.Reports;
using OrderFlow.Application.Features.Orders.GetOrders;
using OrderFlow.Application.Features.Orders.GetOrdersAfter;
using OrderFlow.Application.Features.Orders.ShipOrder;
using OrderFlow.Application.Features.Payments.GetOrderPayments;
using OrderFlow.Application.Features.Payments.GetOrderPaymentSummary;
using OrderFlow.Application.Features.Payments.GetPaymentById;
using OrderFlow.Application.Features.Payments.GetPayments;
using OrderFlow.Application.Features.Payments.ProcessPayment;
using OrderFlow.Application.Features.Payments.ReconcilePayments;
using OrderFlow.Application.Features.Products.CreateProduct;
using OrderFlow.Application.Features.Products.DiscontinueProduct;
using OrderFlow.Application.Features.Products.GetLowStockProducts;
using OrderFlow.Application.Features.Products.GetProductById;
using OrderFlow.Application.Features.Products.GetProducts;
using OrderFlow.Application.Features.Products.ReactivateProduct;
using OrderFlow.Application.Features.Products.ReserveStock;
using OrderFlow.Application.Features.Products.RestockProduct;
using OrderFlow.Application.Features.Products.UpdateProduct;

namespace OrderFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Every command goes through the validation pipeline (OF-10): callers depend on
        // ICommandHandler<,> and get the decorated instance. PipelineCoverageTests fails if a
        // command handler is added without this, or if a controller injects the concrete class.
        services.AddValidatedCommandHandler<RegisterCommand, RegisterResult, RegisterCommandHandler>();
        services.AddValidatedCommandHandler<LoginCommand, LoginResult, LoginCommandHandler>();
        services.AddValidatedCommandHandler<ConfirmEmailOtpCommand, ConfirmEmailOtpResult, ConfirmEmailOtpCommandHandler>();
        services.AddValidatedCommandHandler<CreateStaffUserCommand, CreateStaffUserResult, CreateStaffUserCommandHandler>();

        services.AddValidatedCommandHandler<CreateOrderCommand, CreateOrderResult, CreateOrderCommandHandler>();
        services.AddScoped<GetOrderByIdQueryHandler>();
        services.AddValidatedCommandHandler<CancelOrderCommand, CancelOrderResult, CancelOrderCommandHandler>();
        services.AddValidatedCommandHandler<ConfirmOrderCommand, ConfirmOrderResult, ConfirmOrderCommandHandler>();
        services.AddValidatedCommandHandler<ShipOrderCommand, ShipOrderResult, ShipOrderCommandHandler>();
        services.AddValidatedCommandHandler<DeliverOrderCommand, DeliverOrderResult, DeliverOrderCommandHandler>();
        services.AddValidatedCommandHandler<ProcessPaymentCommand, ProcessPaymentResult, ProcessPaymentCommandHandler>();
        services.AddValidatedCommandHandler<ReconcilePaymentsCommand, ReconcilePaymentsResult, ReconcilePaymentsCommandHandler>();
        services.AddScoped<GetOrderPaymentsQueryHandler>();
        services.AddScoped<GetOrderPaymentSummaryQueryHandler>();
        services.AddScoped<GetPaymentByIdQueryHandler>();
        services.AddScoped<GetPaymentsQueryHandler>();
        services.AddScoped<GetOrdersQueryHandler>();
        services.AddScoped<GetOrdersAfterQueryHandler>();
        services.AddScoped<GetOrderStatusSummaryQueryHandler>();
        services.AddScoped<GetMonthlyRevenueQueryHandler>();
        services.AddScoped<GetTopCustomersQueryHandler>();
        services.AddScoped<GetStalePendingOrdersQueryHandler>();
        services.AddScoped<GetCustomerOrderStatsQueryHandler>();
        services.AddValidatedCommandHandler<CreateCustomerCommand, CreateCustomerResult, CreateCustomerCommandHandler>();
        services.AddScoped<GetCustomerByIdQueryHandler>();
        services.AddScoped<GetCustomersQueryHandler>();
        services.AddValidatedCommandHandler<UpdateCustomerCommand, UpdateCustomerResult, UpdateCustomerCommandHandler>();
        services.AddValidatedCommandHandler<DeactivateCustomerCommand, DeactivateCustomerResult, DeactivateCustomerCommandHandler>();
        services.AddValidatedCommandHandler<ReactivateCustomerCommand, ReactivateCustomerResult, ReactivateCustomerCommandHandler>();
        services.AddValidatedCommandHandler<ChangeCustomerEmailCommand, ChangeCustomerEmailResult, ChangeCustomerEmailCommandHandler>();
        services.AddValidatedCommandHandler<DeleteCustomerCommand, DeleteCustomerResult, DeleteCustomerCommandHandler>();
        services.AddValidatedCommandHandler<CreateProductCommand, CreateProductResult, CreateProductCommandHandler>();
        services.AddScoped<GetProductByIdQueryHandler>();
        services.AddScoped<GetProductsQueryHandler>();
        services.AddValidatedCommandHandler<UpdateProductCommand, UpdateProductResult, UpdateProductCommandHandler>();
        services.AddValidatedCommandHandler<DiscontinueProductCommand, DiscontinueProductResult, DiscontinueProductCommandHandler>();
        services.AddValidatedCommandHandler<RestockProductCommand, RestockProductResult, RestockProductCommandHandler>();
        services.AddValidatedCommandHandler<ReactivateProductCommand, ReactivateProductResult, ReactivateProductCommandHandler>();
        services.AddScoped<GetLowStockProductsQueryHandler>();
        services.AddValidatedCommandHandler<ReserveStockCommand, ReserveStockResult, ReserveStockCommandHandler>();

        return services;
    }
}
