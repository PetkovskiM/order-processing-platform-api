namespace OrderProcessing.Api.Exceptions;

public class BadRequestException : ApiException
{
    public BadRequestException(string message, string errorCode = ErrorCodes.BadRequest)
        : base(message, StatusCodes.Status400BadRequest,errorCode)
    {
    }
}