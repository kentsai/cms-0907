using CMS.API.Infrastructure;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.OpenApi.Models;

const string LocalhostCorsPolicy = "LocalhostCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

// No "Server: Kestrel" advertisement. (IIS adds its own Server / X-Powered-By headers; deploy\CMS.API\web.config.template removes those.)
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllers(options =>
{
    // Every action requires an authenticated user; only AuthController opts out with [AllowAnonymous].
    var authenticatedUser = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(authenticatedUser));
    // A login made with the default password gets a token that only opens the password change (403 elsewhere).
    options.Filters.Add(new PasswordChangeRequiredFilter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "CMS API", Version = "v1" });

    // "Authorize" button in Swagger UI: paste the accessToken from POST /api/auth/login.
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "POST /api/auth/login 回傳的 accessToken。"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = JwtBearerDefaults.AuthenticationScheme }
            },
            Array.Empty<string>()
        }
    });
});

// The Angular dev server runs on a different localhost port, so any loopback origin is allowed.
builder.Services.AddCors(options =>
{
    options.AddPolicy(LocalhostCorsPolicy, policy => policy
        .SetIsOriginAllowed(origin =>
            Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Dapper 2.1 cannot bind DateOnly (Course / FeaturedPromoItem `date` columns) without this handler.
DapperTypeHandlers.Register();
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

// RowAudit records the JWT `userName` claim of the current request (fallback: Identity.Name = `userId`), "system"
// when unauthenticated; timestamps come from the TimeProvider registered below.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRowAuditWriter, RowAuditWriter>();
builder.Services.AddScoped<IRowAuditRepository, RowAuditRepository>();
builder.Services.AddScoped<ILookupRepository, LookupRepository>();

// Login (POST /api/auth/login): credential lookup + JWT issue. The signing key is read from SysConfig per request.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();

// Bearer authentication: validates those tokens with the same SysConfig key (cached, see SigningKeyCache).
builder.Services.AddSingleton<ISigningKeyCache, SigningKeyCache>();
// A token issued before AppUser.PasswordUpdatedTime is rejected (see PasswordStampCache): password change = re-login.
builder.Services.AddSingleton<IPasswordStampCache, PasswordStampCache>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();
// Authentication is the baseline for every action (the global filter above); the endpoints that hand out access
// itself — accounts, passwords, role membership — additionally require the Admin role claim.
builder.Services.AddAuthorization(options => options.AddPolicy(
    AuthorizationPolicies.Admin, policy => policy.RequireRole(AuthorizationPolicies.AdminRole)));

// Feature repositories
builder.Services.AddScoped<IPublishStatusRepository, PublishStatusRepository>();
builder.Services.AddScoped<IAppRoleRepository, AppRoleRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<ICourseGroupRepository, CourseGroupRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<ICertificationRepository, CertificationRepository>();
builder.Services.AddScoped<IFeaturedPromoItemRepository, FeaturedPromoItemRepository>();

var app = builder.Build();

// Very first, so its OnStarting hook is registered for every request: every response that leaves this process —
// including the 500 written by the exception middleware below — carries the security headers (see SecurityHeadersMiddleware).
app.UseMiddleware<SecurityHeadersMiddleware>();

// Wraps everything below: any exception no controller mapped is logged in full and answered with
// 500 + { message, traceId } (see GlobalExceptionMiddleware).
app.UseMiddleware<GlobalExceptionMiddleware>();

// Development only. Both are plain middleware, so the global AuthorizeFilter (an MVC action filter) never runs
// for them: left on, they would publish the whole API surface — every route, schema and admin endpoint — to
// anonymous callers of a deployed environment. Set ASPNETCORE_ENVIRONMENT explicitly on every deploy target.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1"));
}

app.UseCors(LocalhostCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so the test project can reference the entry-point assembly.
public partial class Program;
