namespace OrderFlow.Application.Abstractions.Notifications;

/// <summary>
/// Outbound email. v1 ships a log-only stub (ADR-002) — no real provider is integrated.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
