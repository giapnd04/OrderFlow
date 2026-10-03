using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions.Notifications;

namespace OrderFlow.Infrastructure.ExternalServices;

/// <summary>
/// v1 stand-in for real email delivery (ADR-002): nothing is sent, the message is written
/// to the log so a developer can read the verification code. It logs the full body, which
/// is exactly why this must be replaced before any real deployment.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("STUB EMAIL (not sent) to {To} | {Subject} | {Body}", to, subject, body);

        return Task.CompletedTask;
    }
}
