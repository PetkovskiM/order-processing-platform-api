using Microsoft.AspNetCore.Http;

namespace OrderProcessing.Api.Exceptions;

public class BadRequestException : ApiException
{
    public BadRequestException(string message)
        : base(message, StatusCodes.Status400BadRequest)
    {
    }
}