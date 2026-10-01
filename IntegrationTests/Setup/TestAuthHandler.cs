using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Setup;

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Guid.TryParse(Request.Headers["Test-Org-Id"].ToString(), out var orgId) ||
            !Guid.TryParse(Request.Headers["Test-User-Id"].ToString(), out var userId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Test organization and user headers are required."));
        }

        var role = Request.Headers["Test-Role"].ToString();
        if (string.IsNullOrWhiteSpace(role))
            return Task.FromResult(AuthenticateResult.Fail("Test role header is required."));

        var claims = new[]
        {
            new Claim("org_id", orgId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
