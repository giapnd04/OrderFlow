namespace OrderFlow.Application.Features.Customers.CreateCustomer
{
    public sealed record CreateCustomerResult(int CustomerId,string Name,string Email,string? Phone);

}