using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Models.Customers;
using OrderFlow.Application.Features.Customers.CreateCustomer;
using OrderFlow.Application.Features.Customers.GetCustomerById;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly CreateCustomerCommandHandler _createCustomer;
    private readonly GetCustomerByIdQueryHandler _getCustomerById;

    public CustomersController(
        CreateCustomerCommandHandler createCustomer,
        GetCustomerByIdQueryHandler getCustomerById)
    {
        _createCustomer = createCustomer;
        _getCustomerById = getCustomerById;
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

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GetCustomerByIdResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetCustomerByIdResult>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var query = new GetCustomerByIdQuery(id);

        var result = await _getCustomerById.Handle(query, cancellationToken);

        return Ok(result);
    }
}