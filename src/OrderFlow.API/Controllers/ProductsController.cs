using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Models.Products;
using OrderFlow.Application.Features.Products.CreateProduct;
using OrderFlow.Application.Features.Products.GetProductById;
using OrderFlow.Application.Features.Products.GetProducts;
using OrderFlow.Application.Features.Products.UpdateProduct;
using OrderFlow.Domain.Enums;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly CreateProductCommandHandler _createProduct;
    private readonly GetProductByIdQueryHandler _getProductById;
    private readonly GetProductsQueryHandler _getProducts;
    private readonly UpdateProductCommandHandler _updateProduct;

    public ProductsController(
        CreateProductCommandHandler createProduct,
        GetProductByIdQueryHandler getProductById,
        GetProductsQueryHandler getProducts,
        UpdateProductCommandHandler updateProduct)
    {
        _createProduct = createProduct;
        _getProductById = getProductById;
        _getProducts = getProducts;
        _updateProduct = updateProduct;
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

    [HttpGet]
    [ProducesResponseType(typeof(GetProductsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetProductsResult>> GetProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] ProductStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductsQuery(pageNumber, pageSize, search, status);

        var result = await _getProducts.Handle(query, cancellationToken);

        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UpdateProductResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<UpdateProductResult>> Update(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductCommand(id, request.Name, request.Price);

        var result = await _updateProduct.Handle(command, cancellationToken);

        return Ok(result);
    }
}