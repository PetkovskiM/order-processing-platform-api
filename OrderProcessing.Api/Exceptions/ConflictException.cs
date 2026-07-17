namespace OrderProcessing.Api.Exceptions;

public class ConflictException : ApiException
{
    public ConflictException(string message, string errorCode = ErrorCodes.Conflict)
        : base(message, StatusCodes.Status409Conflict, errorCode)
    {
    }
}