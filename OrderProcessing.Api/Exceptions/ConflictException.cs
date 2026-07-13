using Microsoft.AspNetCore.Http;

namespace OrderProcessing.Api.Exceptions;

public class ConflictException : ApiException
{
    public ConflictException(string message)
        : base(message, StatusCodes.Status409Conflict)
    {
    }
}