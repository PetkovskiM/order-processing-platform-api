using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using OrderProcessing.Api.Exceptions;

namespace OrderProcessing.Api.Extensions;

public static class ValidationExtensions
{
    public static IServiceCollection AddCustomValidationResponse(
        this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problemDetailsFactory =
                    context.HttpContext.RequestServices
                        .GetRequiredService<ProblemDetailsFactory>();

                var problemDetails =
                    problemDetailsFactory.CreateValidationProblemDetails(
                        context.HttpContext,
                        context.ModelState,
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Validation failed",
                        detail: "One or more validation errors occurred.",
                        instance: context.HttpContext.Request.Path);

                problemDetails.AddCommonExtensions(
                    context.HttpContext,
                    ErrorCodes.ValidationFailed);

                return new BadRequestObjectResult(problemDetails)
                {
                    ContentTypes =
                    {
                        "application/problem+json"
                    }
                };
            };
        });

        return services;
    }
}