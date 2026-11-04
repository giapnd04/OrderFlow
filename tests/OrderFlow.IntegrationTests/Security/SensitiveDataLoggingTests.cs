using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.IntegrationTests.Fixtures;

namespace OrderFlow.IntegrationTests.Security;

// OF-07: anyone who can read the logs must not be able to take over an account from them.
[Collection(SqlServerCollection.Name)]
public sealed class SensitiveDataLoggingTests : IAsyncLifetime
{
    private const string Password = "Str0ng!Passw0rd#2026";
    private const string OtpCode = "654321";

    private readonly SqlServerFixture _sql;

    public SensitiveDataLoggingTests(SqlServerFixture sql) => _sql = sql;

    public Task InitializeAsync() => _sql.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisterAndLogin_NeverWriteOtpPasswordOrTokenToTheLog()
    {
        var logs = new CapturingLoggerProvider();

        await using var baseFactory = new SqlApiFactory(_sql.ConnectionString);
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IOtpGenerator>(new FixedOtpGenerator(OtpCode)));
        });

        using var client = factory.CreateClient();

        using var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Log Probe",
            email = "log.probe@example.com",
            password = Password,
        });
        register.EnsureSuccessStatusCode();

        using var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "log.probe@example.com",
            password = Password,
        });
        login.EnsureSuccessStatusCode();

        var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync())
            .RootElement.GetProperty("accessToken").GetString()!;

        var written = logs.Messages;

        Assert.NotEmpty(written);
        Assert.DoesNotContain(written, line => line.Contains(OtpCode));
        Assert.DoesNotContain(written, line => line.Contains(Password));
        Assert.DoesNotContain(written, line => line.Contains(token));
    }

    private sealed class FixedOtpGenerator(string code) : IOtpGenerator
    {
        public string GenerateCode() => code;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception) + exception);
        }
    }
}
