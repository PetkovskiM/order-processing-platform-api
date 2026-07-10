using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Api.DTOs.Customers;
using OrderProcessing.Api.Services.Customers;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _customerService.CreateAsync(request, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = customer.Id },
                customer);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var customers = await _customerService.GetAllAsync(cancellationToken);

        return Ok(customers);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var customer = await _customerService.GetByIdAsync(id, cancellationToken);

        if (customer is null)
        {
            return NotFound(new
            {
                message = $"Customer with id {id} was not found."
            });
        }

        return Ok(customer);
    }
}