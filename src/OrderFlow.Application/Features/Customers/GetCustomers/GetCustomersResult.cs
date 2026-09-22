namespace OrderFlow.Application.Features.Customers.GetCustomers;

public sealed record GetCustomersResult(
    IReadOnlyCollection<GetCustomersItemResult> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record GetCustomersItemResult(
    int CustomerId,
    string Name,
    string Email,
    string? Phone);
