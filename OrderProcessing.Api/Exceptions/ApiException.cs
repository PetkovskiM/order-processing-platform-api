namespace OrderProcessing.Api.Exceptions;

public abstract class ApiException : Exception
{
    protected ApiException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}