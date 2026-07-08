using Microsoft.AspNetCore.Mvc;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Application = "Order Processing API",
            Timestamp = DateTime.UtcNow
        });
    }
}