using Microsoft.Net.Http.Headers;

namespace CMS.API.Infrastructure;

/// <summary>
/// Puts the browser-facing security headers on <b>every</b> response the API sends — 200s, 401s from the bearer
/// handler, 403s from the filters, and the 500 <see cref="GlobalExceptionMiddleware"/> writes. Registered first
/// in <c>Program.cs</c>; the headers are attached in <see cref="HttpResponse.OnStarting"/>, so it does not matter
/// which component eventually produces the response.
/// <para>
/// The API only ever returns JSON, so its Content-Security-Policy is the strictest possible: nothing may load and
/// nothing may frame it. The Swagger UI (Development only, see <c>Program.cs</c>) is a real HTML page with inline
/// script and styles from Swashbuckle, so requests under <see cref="SwaggerPath"/> get a policy that lets it work,
/// and are not marked <c>no-store</c>.
/// </para>
/// <para>
/// Every header is set only when absent, so an action that has a reason to differ can set its own first.
/// <c>Strict-Transport-Security</c> is emitted only on HTTPS requests: browsers ignore it over plain HTTP anyway,
/// and emitting it there would only mislead anyone reading the headers. The Angular site is served by IIS, not
/// this process; its equivalent set lives in <c>deploy\CMS.NG\web.config.template</c>.
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>For JSON responses: no sub-resources of any kind, and no framing.</summary>
    public const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    /// <summary>For the Swagger UI page and its assets, which Swashbuckle serves with inline script and styles.</summary>
    public const string SwaggerContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'";

    /// <summary>One year. Deliberately without <c>includeSubDomains</c>: the API host may share a domain with unrelated sites.</summary>
    public const string StrictTransportSecurity = "max-age=31536000";

    public const string XContentTypeOptions = "nosniff";
    public const string XFrameOptions = "DENY";
    public const string ReferrerPolicyHeaderName = "Referrer-Policy";
    public const string ReferrerPolicy = "no-referrer";

    /// <summary>Responses carry bearer-protected data (and, from login, the token itself); no cache may keep them.</summary>
    public const string CacheControl = "no-store";

    /// <summary>Path prefix of the Swagger document and UI (<c>UseSwagger</c> / <c>UseSwaggerUI</c> defaults).</summary>
    public static readonly PathString SwaggerPath = new("/swagger");

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            Apply((HttpContext)state);
            return Task.CompletedTask;
        }, context);

        return next(context);
    }

    /// <summary>Adds the headers to <paramref name="context"/>'s response; public so the rule can be unit-tested without a host.</summary>
    public static void Apply(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Response.Headers;
        var isSwagger = context.Request.Path.StartsWithSegments(SwaggerPath);

        SetIfAbsent(headers, HeaderNames.XContentTypeOptions, XContentTypeOptions);
        SetIfAbsent(headers, HeaderNames.XFrameOptions, XFrameOptions);
        SetIfAbsent(headers, ReferrerPolicyHeaderName, ReferrerPolicy);
        SetIfAbsent(headers, HeaderNames.ContentSecurityPolicy, isSwagger ? SwaggerContentSecurityPolicy : ContentSecurityPolicy);

        if (!isSwagger)
        {
            SetIfAbsent(headers, HeaderNames.CacheControl, CacheControl);
        }

        if (context.Request.IsHttps)
        {
            SetIfAbsent(headers, HeaderNames.StrictTransportSecurity, StrictTransportSecurity);
        }
    }

    private static void SetIfAbsent(IHeaderDictionary headers, string name, string value)
    {
        if (!headers.ContainsKey(name))
        {
            headers[name] = value;
        }
    }
}
