using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using OrderFlow.API.Controllers;
using OrderFlow.Application;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Behaviors;
using OrderFlow.Infrastructure;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.IntegrationTests.Security;

namespace OrderFlow.IntegrationTests.Observability;

// OF-10 / G-07: every command must go through the validation decorator. These tests fail when a
// new command handler is added without registering it that way, or when a controller asks for
// the concrete handler class and so skips the pipeline.
public sealed class PipelineCoverageTests
{
    private static readonly Type[] CommandHandlerTypes = typeof(OrderFlow.Application.DependencyInjection).Assembly
        .GetTypes()
        .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } && CommandInterfaceOf(type) is not null)
        .ToArray();

    [Fact]
    public void ThereAreCommandHandlersToCheck()
    {
        Assert.True(CommandHandlerTypes.Length >= 20, $"expected 20+ command handlers, found {CommandHandlerTypes.Length}");
    }

    [Fact]
    public void EveryCommandHandler_ResolvesBehindTheValidationDecorator()
    {
        using var provider = BuildServices();
        using var scope = provider.CreateScope();

        var undecorated = CommandHandlerTypes
            .Where(handler =>
            {
                var resolved = scope.ServiceProvider.GetService(CommandInterfaceOf(handler)!);
                return resolved is null
                    || resolved.GetType().GetGenericTypeDefinition() != typeof(ValidationCommandHandlerDecorator<,>);
            })
            .Select(handler => handler.Name)
            .ToList();

        Assert.True(undecorated.Count == 0, "Not behind the validation decorator: " + string.Join(", ", undecorated));
    }

    [Fact]
    public void NoController_DependsOnAConcreteCommandHandler()
    {
        var controllers = typeof(OrdersController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract);

        var offenders = controllers
            .SelectMany(controller => controller.GetConstructors()
                .SelectMany(ctor => ctor.GetParameters())
                .Concat(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(action => action.GetParameters()))
                .Where(parameter => CommandHandlerTypes.Contains(parameter.ParameterType))
                .Select(parameter => $"{controller.Name}({parameter.ParameterType.Name})"))
            .ToList();

        Assert.True(offenders.Count == 0, "Inject ICommandHandler<,> instead: " + string.Join(", ", offenders));
    }

    [Fact]
    public void CommandTimeout_ComesFromConfiguration()
    {
        using var provider = BuildServices(("Database:CommandTimeoutSeconds", "7"));
        using var scope = provider.CreateScope();

        Assert.Equal(7, scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>().Database.GetCommandTimeout());
    }

    private static Type? CommandInterfaceOf(Type type) => type.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>));

    // Resolving never opens a connection, so an unreachable connection string is fine here.
    private static ServiceProvider BuildServices(params (string Key, string Value)[] extra)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:OrderFlow"] = "Server=127.0.0.1,1;Database=Unreachable;Connect Timeout=1",
            ["Jwt:SigningKey"] = ApiFactory.SigningKey,
        };

        foreach (var (key, value) in extra)
        {
            settings[key] = value;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = "Testing" });
        services.AddApplication();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        return services.BuildServiceProvider();
    }
}
