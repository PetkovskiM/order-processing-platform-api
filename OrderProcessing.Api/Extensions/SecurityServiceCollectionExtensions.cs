using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using OrderProcessing.Api.Security;

namespace OrderProcessing.Api.Extensions;

public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));

        services.AddSingleton<IAuthorizationHandler, ScopeOrRoleAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy =
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

            options.AddPolicy(AuthorizationPolicies.ReadAccess,
                policy =>
                {
                    policy.RequireAuthenticatedUser();

                    policy.AddRequirements(
                        new ScopeOrRoleRequirement(
                            acceptedScopes:
                            [
                                ApiScopes.Read,
                                ApiScopes.Write
                            ],
                            acceptedRoles:
                            [
                                ApiRoles.Admin
                            ]));
                });

            options.AddPolicy(AuthorizationPolicies.WriteAccess,
                policy =>
                {
                    policy.RequireAuthenticatedUser();

                    policy.AddRequirements(
                        new ScopeOrRoleRequirement(
                            acceptedScopes:
                            [
                                ApiScopes.Write
                            ],
                            acceptedRoles:
                            [
                                ApiRoles.Admin
                            ]));
                });
        });

        return services;
    }
}