using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OrderProcessing.Api.Extensions;

public static class ProblemDetailsExtensions
{
    public static void AddCommonExtensions(
        this ProblemDetails problemDetails,
        HttpContext httpContext,
        string errorCode)
    {
        problemDetails.Extensions["errorCode"] = errorCode;

        problemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? httpContext.TraceIdentifier;

        problemDetails.Extensions["timestampUtc"] = DateTime.UtcNow;
    }
}