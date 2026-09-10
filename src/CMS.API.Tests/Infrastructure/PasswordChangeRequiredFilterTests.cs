using System.Security.Claims;
using System.Text.Json;
using CMS.API.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace CMS.API.Tests.Infrastructure;

public class PasswordChangeRequiredFilterTests
{
    private readonly PasswordChangeRequiredFilter _filter = new();

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal SignedIn(bool mustChangePassword, string claimValue = JwtTokenIssuer.MustChangePasswordClaimValue)
    {
        var claims = new List<Claim> { new(JwtTokenIssuer.UserIdClaim, "helen") };
        if (mustChangePassword)
        {
            claims.Add(new Claim(JwtTokenIssuer.MustChangePasswordClaim, claimValue));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer", JwtTokenIssuer.UserIdClaim, ClaimTypes.Role));
    }

    private static AuthorizationFilterContext Context(ClaimsPrincipal user, bool actionAllowsPasswordChangeRequired = false)
    {
        var descriptor = new ActionDescriptor();
        if (actionAllowsPasswordChangeRequired)
        {
            descriptor.EndpointMetadata = [new AllowPasswordChangeRequiredAttribute()];
        }

        var httpContext = new DefaultHttpContext { User = user };
        return new AuthorizationFilterContext(new ActionContext(httpContext, new RouteData(), descriptor), []);
    }

    [Fact]
    public void FlaggedToken_OnAnOrdinaryAction_Gets403WithTheMessage()
    {
        var context = Context(SignedIn(mustChangePassword: true));

        _filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(["message"], body.RootElement.EnumerateObject().Select(p => p.Name).ToList());
        Assert.Equal(PasswordChangeRequiredFilter.PasswordChangeRequiredMessage, body.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void FlaggedToken_OnTheAllowedAction_PassesThrough()
    {
        var context = Context(SignedIn(mustChangePassword: true), actionAllowsPasswordChangeRequired: true);

        _filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OrdinaryToken_PassesThrough_Everywhere()
    {
        var plain = Context(SignedIn(mustChangePassword: false));
        var allowed = Context(SignedIn(mustChangePassword: false), actionAllowsPasswordChangeRequired: true);

        _filter.OnAuthorization(plain);
        _filter.OnAuthorization(allowed);

        Assert.Null(plain.Result);
        Assert.Null(allowed.Result);
    }

    [Fact]
    public void AnonymousRequest_PassesThrough_TheAuthorizeFilterDecidesIt()
    {
        var context = Context(Anonymous());

        _filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("yes")]
    public void ClaimWithAnotherValue_DoesNotFlagTheSession(string value)
    {
        var context = Context(SignedIn(mustChangePassword: true, claimValue: value));

        _filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void ClaimValueComparison_IgnoresCase()
    {
        // JwtSecurityTokenHandler may round-trip the boolean claim as "True".
        var context = Context(SignedIn(mustChangePassword: true, claimValue: "True"));

        _filter.OnAuthorization(context);

        Assert.NotNull(context.Result);
    }

    [Fact]
    public void LeavesAnEarlierRejectionAlone()
    {
        var context = Context(SignedIn(mustChangePassword: true));
        var earlier = new UnauthorizedResult();
        context.Result = earlier;

        _filter.OnAuthorization(context);

        Assert.Same(earlier, context.Result);
    }
}
