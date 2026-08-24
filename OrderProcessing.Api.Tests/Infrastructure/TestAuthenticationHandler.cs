using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrderProcessing.Api.Tests.Infrastructure;

public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuthentication";

    public const string UserHeaderName = "X-Test-User";

    public const string ScopesHeaderName = "X-Test-Scopes";

    public const string RolesHeaderName = "X-Test-Roles";

    public TestAuthenticationHandler( IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
                UserHeaderName,
                out var userHeader) ||
            string.IsNullOrWhiteSpace(userHeader.ToString()))
        {
            return Task.FromResult(
                AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                userHeader.ToString()),

            new(
                ClaimTypes.Name,
                "Integration Test User")
        };

        if (Request.Headers.TryGetValue(
                ScopesHeaderName,
                out var scopesHeader) &&
            !string.IsNullOrWhiteSpace(scopesHeader.ToString()))
        {
            claims.Add(
                new Claim(
                    "scp",
                    scopesHeader.ToString()));
        }

        if (Request.Headers.TryGetValue(
                RolesHeaderName,
                out var rolesHeader))
        {
            var roles = rolesHeader
                .ToString()
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);

            claims.AddRange(
                roles.Select(role =>
                    new Claim("roles", role)));
        }

        var identity = new ClaimsIdentity(
            claims,
            SchemeName,
            ClaimTypes.Name,
            "roles");

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(
            principal,
            SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}