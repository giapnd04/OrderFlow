using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Validation;

namespace OrderFlow.Application.Behaviors;

public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="THandler"/> behind the validation decorator. Callers
    /// depend on <c>ICommandHandler&lt;TCommand, TResult&gt;</c> to get the pipeline; the
    /// concrete handler stays registered too, so existing code that injects it directly keeps working.
    /// </summary>
    public static IServiceCollection AddValidatedCommandHandler<TCommand, TResult, THandler>(
        this IServiceCollection services)
        where TCommand : ICommand<TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        services.AddScoped<THandler>();

        services.AddScoped<ICommandHandler<TCommand, TResult>>(provider =>
            new ValidationCommandHandlerDecorator<TCommand, TResult>(
                provider.GetRequiredService<THandler>(),
                provider.GetServices<IValidator<TCommand>>()));

        return services;
    }

    /// <summary>Registers every non-abstract <see cref="IValidator{T}"/> found in <paramref name="assembly"/>.</summary>
    public static IServiceCollection AddValidatorsFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var registrations = assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
                .Select(i => (Service: i, Implementation: type)));

        foreach (var (service, implementation) in registrations)
        {
            services.AddScoped(service, implementation);
        }

        return services;
    }
}
