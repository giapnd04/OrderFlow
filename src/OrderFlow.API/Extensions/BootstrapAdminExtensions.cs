using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Auth.CreateStaffUser;
using OrderFlow.Domain.Enums;

namespace OrderFlow.API.Extensions;

public static class BootstrapAdminExtensions
{
    /// <summary>
    /// Creates the first Administrator from <c>Bootstrap:AdminEmail</c> / <c>Bootstrap:AdminPassword</c>
    /// (use environment variables or user-secrets - never a committed file), because nobody can
    /// create staff accounts until one Administrator exists. Does nothing if either value is
    /// unset or the account already exists, so it is safe to leave configured. A weak password
    /// fails startup loudly rather than silently creating a weak admin.
    /// </summary>
    public static async Task SeedBootstrapAdminAsync(this WebApplication app)
    {
        var email = app.Configuration["Bootstrap:AdminEmail"];
        var password = app.Configuration["Bootstrap:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return;
        }

        using var scope = app.Services.CreateScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<CreateStaffUserCommand, CreateStaffUserResult>>();

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("BootstrapAdmin");

        try
        {
            var created = await handler.Handle(new CreateStaffUserCommand(email, password, UserRole.Administrator));

            logger.LogInformation("Bootstrap administrator {Email} created (user {UserId}).", created.Email, created.UserId);
        }
        catch (ConflictException)
        {
            logger.LogInformation("Bootstrap administrator {Email} already exists; nothing to do.", email);
        }
    }
}
