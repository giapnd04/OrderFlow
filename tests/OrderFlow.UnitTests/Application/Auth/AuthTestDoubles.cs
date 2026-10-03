using OrderFlow.Application.Abstractions.Notifications;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Domain.Entities;

namespace OrderFlow.UnitTests.Application.Auth;

internal sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FixedTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public int HashCalls { get; private set; }

    public string Hash(string password)
    {
        HashCalls++;
        return "hashed:" + password;
    }

    public bool Verify(string password, string passwordHash) => passwordHash == "hashed:" + password;
}

internal sealed class FixedOtpGenerator : IOtpGenerator
{
    private readonly string _code;

    public FixedOtpGenerator(string code = "123456") => _code = code;

    public string GenerateCode() => _code;
}

internal sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
    public User? LastUser { get; private set; }

    public AccessToken Generate(User user)
    {
        LastUser = user;
        return new AccessToken($"token-for-{user.Id}", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}

internal sealed class RecordingEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    public Exception? FailWith { get; set; }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

/// <summary>Runs the action inline and records whether it completed - the unit-test stand-in for a DB transaction.</summary>
internal sealed class FakeTransactionRunner : ITransactionRunner
{
    public int Runs { get; private set; }

    public int Failures { get; private set; }

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        Runs++;

        try
        {
            await action(cancellationToken);
        }
        catch
        {
            Failures++;
            throw;
        }
    }
}

internal sealed class FakeUserRepository : IUserRepository
{
    private int _nextId = 1;

    public List<User> Users { get; } = new();

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Email == email));

    public Task<User?> GetByIdForUpdateAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.Any(u => u.Email == email));

    public Task<bool> IsCustomerLinkedAsync(int customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.Any(u => u.CustomerId == customerId));

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        user.Id = _nextId++;
        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeOtpRepository : IEmailVerificationOtpRepository
{
    private int _nextId = 1;

    public List<EmailVerificationOtp> Otps { get; } = new();

    public Task AddAsync(EmailVerificationOtp otp, CancellationToken cancellationToken = default)
    {
        otp.Id = _nextId++;
        Otps.Add(otp);
        return Task.CompletedTask;
    }

    public Task<EmailVerificationOtp?> GetLatestUsableForUpdateAsync(
        int userId, DateTime nowUtc, CancellationToken cancellationToken = default)
        => Task.FromResult(Otps
            .Where(o => o.UserId == userId && o.IsUsable(nowUtc))
            .OrderByDescending(o => o.Id)
            .FirstOrDefault());

    public Task UpdateAsync(EmailVerificationOtp otp, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Customer repository for auth tests: supports lookup by email and add, nothing else.</summary>
internal sealed class AuthFakeCustomerRepository : ICustomerRepository
{
    private int _nextId = 100;

    public List<Customer> Customers { get; } = new();

    public Customer Seed(string name, string email)
    {
        var customer = Customer.Create(name, email);
        customer.Id = _nextId++;
        Customers.Add(customer);
        return customer;
    }

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Customers.FirstOrDefault(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        customer.Id = _nextId++;
        Customers.Add(customer);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Customer?> GetByIdForUpdateAsync(int customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<(IReadOnlyCollection<Customer> Customers, int TotalCount)> GetPagedAsync(
        string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task DeleteAsync(Customer customer, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
