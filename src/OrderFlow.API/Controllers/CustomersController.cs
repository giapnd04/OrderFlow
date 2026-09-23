using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Models.Customers;
using OrderFlow.Application.Features.Customers.CreateCustomer;
using OrderFlow.Application.Features.Customers.DeactivateCustomer;
using OrderFlow.Application.Features.Customers.GetCustomerById;
using OrderFlow.Application.Features.Customers.GetCustomers;
using OrderFlow.Application.Features.Customers.ReactivateCustomer;
using OrderFlow.Application.Features.Customers.UpdateCustomer;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly CreateCustomerCommandHandler _createCustomer;
    private readonly GetCustomerByIdQueryHandler _getCustomerById;
    private readonly GetCustomersQueryHandler _getCustomers;
    private readonly UpdateCustomerCommandHandler _updateCustomer;
    private readonly DeactivateCustomerCommandHandler _deactivateCustomer;
    private readonly ReactivateCustomerCommandHandler _reactivateCustomer;

    public CustomersController(
        CreateCustomerCommandHandler createCustomer,
        GetCustomerByIdQueryHandler getCustomerById,
        GetCustomersQueryHandler getCustomers,
        UpdateCustomerCommandHandler updateCustomer,
        DeactivateCustomerCommandHandler deactivateCustomer,
        ReactivateCustomerCommandHandler reactivateCustomer)
    {
        _createCustomer = createCustomer;
        _getCustomerById = getCustomerById;
        _getCustomers = getCustomers;
        _updateCustomer = updateCustomer;
        _deactivateCustomer = deactivateCustomer;
        _reactivateCustomer = reactivateCustomer;
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

    [HttpGet]
    [ProducesResponseType(typeof(GetCustomersResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetCustomersResult>> GetCustomers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCustomersQuery(pageNumber, pageSize, search);

        var result = await _getCustomers.Handle(query, cancellationToken);

        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UpdateCustomerResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<UpdateCustomerResult>> Update(
        int id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCustomerCommand(id, request.Name, request.Phone);

        var result = await _updateCustomer.Handle(command, cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(
        int id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateCustomerCommand(id);

        await _deactivateCustomer.Handle(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:int}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reactivate(
        int id,
        CancellationToken cancellationToken)
    {
        var command = new ReactivateCustomerCommand(id);

        await _reactivateCustomer.Handle(command, cancellationToken);

        return NoContent();
    }
}