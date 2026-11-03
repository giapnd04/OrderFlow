namespace OrderFlow.Application.Abstractions.Payments;

/// <summary>A charge the payment provider says it has actually collected.</summary>
public sealed record CapturedTransaction(
    string Provider,
    string ProviderTransactionId,
    int OrderId,
    decimal Amount);

/// <summary>
/// The provider's own record of collected money: the source of truth that reconciliation
/// compares our payments against (OF-06). Simulated today, a provider API in a real system.
/// </summary>
public interface IPaymentProviderLedger
{
    Task<IReadOnlyList<CapturedTransaction>> GetCapturedTransactionsAsync(CancellationToken cancellationToken = default);
}
