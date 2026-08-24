using Microsoft.AspNetCore.Authorization;

namespace OrderProcessing.Api.Security;

public sealed class ScopeOrRoleRequirement : IAuthorizationRequirement
{
    public ScopeOrRoleRequirement(IEnumerable<string> acceptedScopes, IEnumerable<string> acceptedRoles)
    {
        AcceptedScopes = acceptedScopes.ToHashSet(StringComparer.Ordinal);

        AcceptedRoles = acceptedRoles.ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlySet<string> AcceptedScopes { get; }

    public IReadOnlySet<string> AcceptedRoles { get; }
}