using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OrderProcessing.Api.OpenApi;

// This makes Swagger display locks only on endpoints carrying authorization metadata. The runtime policies remain the real security enforcement. Operation transformers are the recommended approach when security metadata depends on individual endpoints.
public sealed class AuthorizationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();

        var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();

        if (allowsAnonymous || !requiresAuthorization)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];

        operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("oauth2", context.Document)] = []
            });

        return Task.CompletedTask;
    }
}