using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using OrderProcessing.Api.Exceptions;
using OrderProcessing.Api.Extensions;

namespace OrderProcessing.Api.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        ProblemDetailsFactory problemDetailsFactory)
    {
        _next = next;
        _logger = logger;
        _problemDetailsFactory = problemDetailsFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(
                ex,
                "Handled API exception. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}",
                ex.StatusCode,
                ex.ErrorCode);

            await WriteProblemDetailsAsync(
                context,
                ex.StatusCode,
                GetTitle(ex.StatusCode),
                ex.Message,
                ex.ErrorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception occurred while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred.",
                ErrorCodes.InternalServerError);
        }
    }

    private async Task WriteProblemDetailsAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string errorCode)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Cannot write error response because the response has already started.");

            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = _problemDetailsFactory.CreateProblemDetails(
            context,
            statusCode: statusCode,
            title: title,
            detail: detail,
            instance: context.Request.Path);

        problemDetails.AddCommonExtensions(context, errorCode);

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken: context.RequestAborted);
    }

    private static string GetTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            _ => "Error"
        };
    }
}