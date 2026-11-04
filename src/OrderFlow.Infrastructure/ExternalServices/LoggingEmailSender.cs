using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions.Notifications;

namespace OrderFlow.Infrastructure.ExternalServices;

/// <summary>
/// v1 stand-in for real email delivery (ADR-002): nothing is sent. The body holds the
/// verification code, so it is logged only in Development, where a developer needs to read
/// it; anywhere else a log reader could use it to verify someone else's account (OF-07).
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    private readonly IHostEnvironment _environment;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("STUB EMAIL (not sent) to {To} | {Subject} | {Body}", to, subject, body);
        }
        else
        {
            _logger.LogInformation("STUB EMAIL (not sent) to {To} | {Subject}", to, subject);
        }

        return Task.CompletedTask;
    }
}
