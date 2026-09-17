using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Models.Products;
using OrderFlow.Application.Features.Products.CreateProduct;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly CreateProductCommandHandler _createProduct;

    public ProductsController(
        CreateProductCommandHandler createProduct)
    {
        _createProduct = createProduct;
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
}