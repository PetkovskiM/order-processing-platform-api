using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using OrderProcessing.Api.Security;

namespace OrderProcessing.Api.OpenApi;

public sealed class EntraOAuthDocumentTransformer(IConfiguration configuration) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var instance = GetRequiredConfigurationValue("AzureAd:Instance").TrimEnd('/');

        var tenantId = GetRequiredConfigurationValue("AzureAd:TenantId");

        var apiClientId = GetRequiredConfigurationValue("AzureAd:ClientId");

        var readScope = $"api://{apiClientId}/{ApiScopes.Read}";

        var writeScope = $"api://{apiClientId}/{ApiScopes.Write}";

        document.Components ??= new OpenApiComponents();

        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["oauth2"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri($"{instance}/{tenantId}/oauth2/v2.0/authorize"),

                            TokenUrl = new Uri($"{instance}/{tenantId}/oauth2/v2.0/token"),

                            Scopes = new Dictionary<string, string>
                            {
                                [readScope] = "Read order-processing resources.",

                                [writeScope] = "Create or modify order-processing resources."
                            }
                        }
                    }
                }
            };

        return Task.CompletedTask;
    }

    private string GetRequiredConfigurationValue(string key)
    {
        return configuration[key] ?? throw new InvalidOperationException($"Configuration value '{key}' is required.");
    }
}