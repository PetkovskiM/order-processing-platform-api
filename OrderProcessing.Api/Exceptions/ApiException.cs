namespace OrderProcessing.Api.Exceptions;

public abstract class ApiException : Exception
{
    protected ApiException(string message,int statusCode,string errorCode)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    public int StatusCode { get; }

    public string ErrorCode { get; }
}