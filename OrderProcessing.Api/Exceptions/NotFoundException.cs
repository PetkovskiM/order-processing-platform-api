using Microsoft.AspNetCore.Http;

namespace OrderProcessing.Api.Exceptions;

public class NotFoundException : ApiException
{
    public NotFoundException(string message)
        : base(message, StatusCodes.Status404NotFound)
    {
    }
}