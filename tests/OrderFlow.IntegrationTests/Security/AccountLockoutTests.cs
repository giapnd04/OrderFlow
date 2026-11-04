using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.IntegrationTests.Fixtures;

namespace OrderFlow.IntegrationTests.Security;

// OF-07: per-IP rate limiting cannot stop one account being guessed from many addresses.
// SqlApiFactory lifts the per-IP limit, so only the per-account lockout is under test here.
[Collection(SqlServerCollection.Name)]
public sealed class AccountLockoutTests : IAsyncLifetime
{
    private const string Email = "victim@example.com";
    private const string Password = "Str0ng!Passw0rd#2026";
    private const string WrongPassword = "Wr0ng!Passw0rd#2026";

    private readonly SqlServerFixture _sql;
    private SqlApiFactory? _factory;
    private HttpClient? _client;

    public AccountLockoutTests(SqlServerFixture sql) => _sql = sql;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Not initialized.");

    public async Task InitializeAsync()
    {
        await _sql.ResetDatabaseAsync();

        _factory = new SqlApiFactory(_sql.ConnectionString);
        _client = _factory.CreateClient();

        await RegisterAsync(Email);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task AfterMaxFailures_EvenTheRightPasswordGets429WithRetryAfter()
    {
        for (var i = 0; i < LoginThrottlePolicy.MaxFailures; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(Email, WrongPassword)).Status);
        }

        var (status, retryAfter) = await LoginAsync(Email, Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, status);
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, 1, LoginThrottlePolicy.LockDuration.TotalSeconds);
    }

    [Fact]
    public async Task LockingOneAccount_DoesNotAffectAnother()
    {
        await RegisterAsync("bystander@example.com");
        await FailMaxTimesAsync(Email);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("bystander@example.com", Password)).Status);
    }

    [Fact]
    public async Task SuccessfulLogin_ClearsTheCounter()
    {
        for (var i = 0; i < LoginThrottlePolicy.MaxFailures - 1; i++)
        {
            await LoginAsync(Email, WrongPassword);
        }

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Email, Password)).Status);

        for (var i = 0; i < LoginThrottlePolicy.MaxFailures - 1; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(Email, WrongPassword)).Status);
        }

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Email, Password)).Status);
    }

    [Fact]
    public async Task UnknownEmail_BehavesExactlyLikeAKnownOne()
    {
        const string unknown = "nobody@example.com";

        for (var i = 0; i < LoginThrottlePolicy.MaxFailures; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(unknown, WrongPassword)).Status);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(unknown, WrongPassword)).Status);
    }

    [Fact]
    public async Task WhenTheLockExpires_TheRightPasswordWorksAgain()
    {
        await FailMaxTimesAsync(Email);

        await using (var db = _sql.CreateDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE login_throttles
                SET locked_until = DATEADD(MINUTE, -1, SYSUTCDATETIME()),
                    window_started_at = DATEADD(HOUR, -1, SYSUTCDATETIME())
                WHERE email = {Email}
                """);
        }

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Email, Password)).Status);
    }

    [Fact]
    public async Task ParallelFailures_AreAllCounted_AndLockTheAccount()
    {
        var results = await Concurrently.RunAsync(20, () => LoginAsync(Email, WrongPassword));

        Assert.All(results, r => Assert.Contains(r.Status, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests }));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(Email, Password)).Status);

        // Every failure that got past the lock check was counted; none was lost to a race.
        await using var db = _sql.CreateDbContext();
        var counted = await db.Database
            .SqlQuery<int>($"SELECT failed_count AS [Value] FROM login_throttles WHERE email = {Email}")
            .SingleAsync();

        Assert.Equal(results.Count(r => r.Status == HttpStatusCode.Unauthorized), counted);
    }

    private async Task FailMaxTimesAsync(string email)
    {
        for (var i = 0; i < LoginThrottlePolicy.MaxFailures; i++)
        {
            await LoginAsync(email, WrongPassword);
        }
    }

    private async Task RegisterAsync(string email)
    {
        using var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Lockout Probe",
            email,
            password = Password,
        });

        response.EnsureSuccessStatusCode();
    }

    private async Task<(HttpStatusCode Status, double? RetryAfter)> LoginAsync(string email, string password)
    {
        using var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password });

        return (response.StatusCode, response.Headers.RetryAfter?.Delta?.TotalSeconds);
    }
}
