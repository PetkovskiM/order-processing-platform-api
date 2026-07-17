namespace OrderProcessing.Api.Exceptions;

public class NotFoundException : ApiException
{
    public NotFoundException(string message, string errorCode = ErrorCodes.ResourceNotFound)
        : base(message, StatusCodes.Status404NotFound, errorCode)
    {
    }
}