using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Products.CreateProduct;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Application.Tests.Features.Products.CreateProduct;

public sealed class CreateProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreateProduct_WhenCommandIsValid()
    {
        // Arrange
        var repository = new FakeProductRepository();

        var handler = new CreateProductCommandHandler(repository);

        var command = new CreateProductCommand(
            "SKU-001",
            "Laptop",
            25000000m,
            10);

        // Act
        var result = await handler.Handle(command);

        // Assert
        Assert.True(result.ProductId > 0);
        Assert.Equal("SKU-001", result.Sku);
        Assert.Equal("Laptop", result.Name);
        Assert.Equal(25000000m, result.Price);
        Assert.Equal(10, result.StockQuantity);
        Assert.Equal(ProductStatus.Active, result.Status);

        Assert.Equal(1, repository.AddCallCount);
        Assert.NotNull(repository.AddedProduct);
    }

    [Fact]
    public async Task Handle_ShouldTrimSkuAndName_WhenInputContainsWhitespace()
    {
        // Arrange
        var repository = new FakeProductRepository();

        var handler = new CreateProductCommandHandler(repository);

        var command = new CreateProductCommand(
            "  SKU-001  ",
            "  Laptop  ",
            25000000m,
            10);

        // Act
        var result = await handler.Handle(command);

        // Assert
        Assert.Equal("SKU-001", result.Sku);
        Assert.Equal("Laptop", result.Name);

        Assert.Equal(1, repository.AddCallCount);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenSkuIsEmpty()
    {
        // Arrange
        var repository = new FakeProductRepository();

        var handler = new CreateProductCommandHandler(repository);

        var command = new CreateProductCommand(
            "",
            "Laptop",
            25000000m,
            10);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command));

        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenNameIsEmpty()
    {
        // Arrange
        var repository = new FakeProductRepository();

        var handler = new CreateProductCommandHandler(repository);

        var command = new CreateProductCommand(
            "SKU-001",
            "",
            25000000m,
            10);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command));

        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task Handle_ShouldThrowDomainException_WhenPriceIsNegative()
    {
        // Arrange
        var repository = new FakeProductRepository();

        var handler = new CreateProductCommandHandler(repository);

        var command = new CreateProductCommand(
            "SKU-001",
            "Laptop",
            -1m,
            10);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command));

        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task Handle_ShouldThrowDomainException_WhenStockQuantityIsNegative()
    {
        // Arrange
        var repository = new FakeProductRepository();

        var handler = new CreateProductCommandHandler(repository);

        var command = new CreateProductCommand(
            "SKU-001",
            "Laptop",
            25000000m,
            -1);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command));

        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenSkuAlreadyExists()
    {
        // Arrange
        var repository = new FakeProductRepository
        {
            SkuExists = true
        };

        var handler = new CreateProductCommandHandler(repository);

        var command = new CreateProductCommand(
            "SKU-001",
            "Laptop",
            25000000m,
            10);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command));

        Assert.Equal(0, repository.AddCallCount);
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private int _nextId = 1;

        public bool SkuExists { get; set; }

        public int AddCallCount { get; private set; }

        public Product? AddedProduct { get; private set; }

        public Task<IReadOnlyList<Product>> GetByIdsAsync(
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Product>>(
                Array.Empty<Product>());
        }

        public Task<Product?> GetByIdAsync(
            int productId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not needed for CreateProduct tests.");
        }

        public Task<bool> ExistsBySkuAsync(
            string sku,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SkuExists);
        }

        public Task AddAsync(
            Product product,
            CancellationToken cancellationToken = default)
        {
            AddCallCount++;

            AddedProduct = product;

            // Simulate database-generated identity.
            // In the real application SQL Server generates the Id.
            typeof(Product)
                .GetProperty(nameof(Product.Id))?
                .SetValue(product, _nextId++);

            return Task.CompletedTask;
        }
    }
}