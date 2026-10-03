using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Validation;
using OrderFlow.Application.Behaviors;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Auth.Login;

namespace OrderFlow.UnitTests.Application.Behaviors;

public sealed class ValidationPipelineTests
{
    private sealed record PingCommand(string Text) : ICommand<string>;

    private sealed class PingHandler : ICommandHandler<PingCommand, string>
    {
        public int Calls { get; private set; }

        public Task<string> Handle(PingCommand command, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult("pong:" + command.Text);
        }
    }

    private sealed class NotEmptyValidator : Validator<PingCommand>
    {
        protected override void Check(PingCommand command, ErrorCollector errors)
            => errors.AddIf(string.IsNullOrWhiteSpace(command.Text), "Text", "Text is required.");
    }

    private sealed class NotLongValidator : Validator<PingCommand>
    {
        protected override void Check(PingCommand command, ErrorCollector errors)
            => errors.AddIf(command.Text?.Length > 5, "Text", "Text is too long.");
    }

    [Fact]
    public async Task Handle_ValidCommand_RunsTheInnerHandler()
    {
        var inner = new PingHandler();
        var decorator = new ValidationCommandHandlerDecorator<PingCommand, string>(inner, new IValidator<PingCommand>[] { new NotEmptyValidator() });

        var result = await decorator.Handle(new PingCommand("hi"));

        Assert.Equal("pong:hi", result);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Handle_InvalidCommand_ThrowsAndNeverReachesTheInnerHandler()
    {
        var inner = new PingHandler();
        var decorator = new ValidationCommandHandlerDecorator<PingCommand, string>(inner, new IValidator<PingCommand>[] { new NotEmptyValidator() });

        var ex = await Assert.ThrowsAsync<ValidationException>(() => decorator.Handle(new PingCommand(" ")));

        Assert.Equal(0, inner.Calls);
        Assert.Equal(new[] { "Text is required." }, ex.Errors["Text"]);
    }

    [Fact]
    public async Task Handle_FailuresFromSeveralValidators_AreAllReported()
    {
        var decorator = new ValidationCommandHandlerDecorator<PingCommand, string>(
            new PingHandler(),
            new IValidator<PingCommand>[] { new NotEmptyValidator(), new NotLongValidator() });

        var ex = await Assert.ThrowsAsync<ValidationException>(() => decorator.Handle(new PingCommand("")));
        Assert.Single(ex.Errors["Text"]);

        var ex2 = await Assert.ThrowsAsync<ValidationException>(() => decorator.Handle(new PingCommand("way too long")));
        Assert.Equal(new[] { "Text is too long." }, ex2.Errors["Text"]);
    }

    [Fact]
    public async Task Handle_NoValidatorsRegistered_JustRunsTheHandler()
    {
        var decorator = new ValidationCommandHandlerDecorator<PingCommand, string>(new PingHandler(), Array.Empty<IValidator<PingCommand>>());

        Assert.Equal("pong:", await decorator.Handle(new PingCommand("")));
    }

    [Fact]
    public void ValidationException_FromFailures_GroupsMessagesByProperty()
    {
        var ex = new ValidationException(new[]
        {
            new ValidationFailure("A", "a1"),
            new ValidationFailure("A", "a2"),
            new ValidationFailure("B", "b1"),
        });

        Assert.Equal(new[] { "a1", "a2" }, ex.Errors["A"]);
        Assert.Equal(new[] { "b1" }, ex.Errors["B"]);
        Assert.Equal("a1 a2 b1", ex.Message);
    }

    [Fact]
    public void ValidationException_FromSingleMessage_HasNoGroupedErrors()
    {
        Assert.Empty(new ValidationException("plain").Errors);
    }

    [Fact]
    public void AddValidatedCommandHandler_ResolvesTheDecoratedHandlerThroughTheInterface()
    {
        var services = new ServiceCollection();
        services.AddScoped<IValidator<PingCommand>, NotEmptyValidator>();
        services.AddValidatedCommandHandler<PingCommand, string, PingHandler>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var viaInterface = scope.ServiceProvider.GetRequiredService<ICommandHandler<PingCommand, string>>();
        var viaConcrete = scope.ServiceProvider.GetRequiredService<PingHandler>();

        Assert.IsType<ValidationCommandHandlerDecorator<PingCommand, string>>(viaInterface);
        Assert.IsType<PingHandler>(viaConcrete);
    }

    [Fact]
    public void AddApplication_RegistersAuthHandlersBehindTheValidationPipeline()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var descriptor = services.Last(d => d.ServiceType == typeof(ICommandHandler<LoginCommand, LoginResult>));

        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<LoginCommand>)
                                       && d.ImplementationType == typeof(LoginCommandValidator));
    }
}
