using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Models.Products;
using OrderFlow.Application.Features.Products.CreateProduct;
using OrderFlow.Application.Features.Products.GetProductById;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly CreateProductCommandHandler _createProduct;
    private readonly GetProductByIdQueryHandler _getProductById;

    public ProductsController(
        CreateProductCommandHandler createProduct,
        GetProductByIdQueryHandler getProductById)
    {
        _createProduct = createProduct;
        _getProductById = getProductById;
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(CreateProductResult),
        StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateProductResult>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand(
            request.Sku,
            request.Name,
            request.Price,
            request.StockQuantity);

        var result = await _createProduct.Handle(
            command,
            cancellationToken);

        return Created(
            $"api/products/{result.ProductId}",
            result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GetProductByIdResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetProductByIdResult>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var query = new GetProductByIdQuery(id);

        var result = await _getProductById.Handle(query, cancellationToken);

        return Ok(result);
    }
}