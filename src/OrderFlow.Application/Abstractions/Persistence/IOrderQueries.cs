using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>One row of an order list: only the columns the list shows, never the whole aggregate.</summary>
public sealed record OrderListItem(int OrderId, int CustomerId, OrderStatus Status, decimal TotalAmount);

/// <summary>
/// Read side of order listing (OF-09), kept apart from <see cref="IOrderRepository"/>, which loads
/// and saves the Order aggregate. Both queries project straight to <see cref="OrderListItem"/>.
/// </summary>
public interface IOrderQueries
{
    /// <summary>Offset paging: page N of the filtered list, plus the total row count.</summary>
    Task<(IReadOnlyList<OrderListItem> Items, int TotalCount)> GetPageAsync(
        OrderStatus? status,
        int? customerId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Keyset paging: up to <paramref name="take"/> orders with an id greater than
    /// <paramref name="afterId"/>, in id order. No count, and the cost does not grow with depth.
    /// </summary>
    Task<IReadOnlyList<OrderListItem>> GetPageAfterAsync(
        OrderStatus? status,
        int? customerId,
        int? afterId,
        int take,
        CancellationToken cancellationToken = default);
}
