using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Models.Customers;
using OrderFlow.Application.Features.Customers.CreateCustomer;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly CreateCustomerCommandHandler _createCustomer;

    public CustomersController(
        CreateCustomerCommandHandler createCustomer)
    {
        _createCustomer = createCustomer;
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(CreateCustomerResult),
        StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateCustomerResult>> Create(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCustomerCommand(
            request.Name,
            request.Email,
            request.Phone);

        var result = await _createCustomer.Handle(
            command,
            cancellationToken);

        return Created(
            $"api/customers/{result.CustomerId}",
            result);
    }
}