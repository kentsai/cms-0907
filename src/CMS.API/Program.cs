using CMS.API.Infrastructure;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.OpenApi.Models;

const string LocalhostCorsPolicy = "LocalhostCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    // Every action requires an authenticated user; only AuthController opts out with [AllowAnonymous].
    var authenticatedUser = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(authenticatedUser));
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

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

// RowAudit records User.Identity.Name — the JWT `userId` claim once authenticated, "system" otherwise.
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
builder.Services.AddAuthorization();

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

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1"));

app.UseCors(LocalhostCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so the test project can reference the entry-point assembly.
public partial class Program;
