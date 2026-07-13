using OrderProcessing.Api.DTOs.Customers;

namespace OrderProcessing.Api.Services.Customers;

public interface ICustomerService
{
    Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerResponse>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<CustomerResponse> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);
}