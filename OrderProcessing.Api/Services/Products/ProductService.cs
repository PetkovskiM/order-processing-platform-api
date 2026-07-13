using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.DTOs.Products;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Exceptions;

namespace OrderProcessing.Api.Services.Products;

public class ProductService : IProductService
{
    private readonly OrderProcessingDbContext _dbContext;

    public ProductService(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        var skuAlreadyExists = await _dbContext.Products
            .AnyAsync(p => p.Sku == normalizedSku, cancellationToken);

        if (skuAlreadyExists)
        {
            throw new ConflictException("A product with this SKU already exists.");
        }

        var product = new Product
        {
            Sku = normalizedSku,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Products.Add(product);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(product);
    }

    public async Task<IReadOnlyList<ProductResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new ProductResponse
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                CreatedAtUtc = p.CreatedAtUtc,
                UpdatedAtUtc = p.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductResponse> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductResponse
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                CreatedAtUtc = p.CreatedAtUtc,
                UpdatedAtUtc = p.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Product not found.");
    }

    public async Task<ProductResponse> UpdateAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("Product not found.");
        }

        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        var skuAlreadyExists = await _dbContext.Products
            .AnyAsync(p => p.Id != id && p.Sku == normalizedSku, cancellationToken);

        if (skuAlreadyExists)
        {
            throw new ConflictException("A product with this SKU already exists.");
        }

        product.Sku = normalizedSku;
        product.Name = request.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(product);
    }

    private static ProductResponse MapToResponse(Product product)
    {
        return new ProductResponse
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            CreatedAtUtc = product.CreatedAtUtc,
            UpdatedAtUtc = product.UpdatedAtUtc
        };
    }
}