using OrderProcessing.Api.DTOs.Products;

namespace OrderProcessing.Api.Services.Products;

public interface IProductService
{
    Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductResponse>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ProductResponse> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> UpdateAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);
}