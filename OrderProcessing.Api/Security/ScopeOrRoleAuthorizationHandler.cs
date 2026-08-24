using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace OrderProcessing.Api.Security;

public sealed class ScopeOrRoleAuthorizationHandler : AuthorizationHandler<ScopeOrRoleRequirement>
{
    private const string ScopeClaimType = "scp";

    private const string MappedScopeClaimType = "http://schemas.microsoft.com/identity/claims/scope";

    private const string RoleClaimType = "roles";

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeOrRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var scopes = context.User.Claims.Where(claim =>
                claim.Type == ScopeClaimType ||
                claim.Type == MappedScopeClaimType)
            .SelectMany(claim =>
                claim.Value.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries));

        var roles = context.User.Claims.Where(claim =>
                claim.Type == RoleClaimType ||
                claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value);

        var hasAcceptedScope = scopes.Any(
            requirement.AcceptedScopes.Contains);

        var hasAcceptedRole = roles.Any(
            requirement.AcceptedRoles.Contains);

        if (hasAcceptedScope || hasAcceptedRole)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}