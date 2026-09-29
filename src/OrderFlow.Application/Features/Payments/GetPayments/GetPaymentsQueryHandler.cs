using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Payments.GetPayments;

public sealed class GetPaymentsQueryHandler
    : IQueryHandler<GetPaymentsQuery, GetPaymentsResult>
{
    private const int MaxPageSize = 100;

    private readonly IPaymentRepository _payments;

    public GetPaymentsQueryHandler(IPaymentRepository payments)
    {
        _payments = payments;
    }

    public async Task<GetPaymentsResult> Handle(
        GetPaymentsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.PageNumber <= 0)
        {
            throw new ValidationException("PageNumber must be greater than zero.");
        }

        if (query.PageSize <= 0)
        {
            throw new ValidationException("PageSize must be greater than zero.");
        }

        if (query.PageSize > MaxPageSize)
        {
            throw new ValidationException($"PageSize cannot be greater than {MaxPageSize}.");
        }

        var (payments, totalCount) = await _payments.GetPagedAsync(
            query.Status,
            query.Provider,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / query.PageSize);

        var items = payments
            .Select(payment => new GetPaymentsItemResult(
                payment.Id,
                payment.OrderId,
                payment.Amount,
                payment.Status.ToString(),
                payment.Provider,
                payment.ProviderTransactionId))
            .ToList();

        return new GetPaymentsResult(items, query.PageNumber, query.PageSize, totalCount, totalPages);
    }
}
