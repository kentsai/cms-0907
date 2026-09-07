using CMS.API.Infrastructure;
using CMS.API.Repositories;
using Microsoft.OpenApi.Models;

const string LocalhostCorsPolicy = "LocalhostCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "CMS API", Version = "v1" });
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

// RowAudit needs the current user name; there is no auth yet so it falls back to "system".
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRowAuditWriter, RowAuditWriter>();
builder.Services.AddScoped<IRowAuditRepository, RowAuditRepository>();
builder.Services.AddScoped<ILookupRepository, LookupRepository>();

// Feature repositories
builder.Services.AddScoped<IPublishStatusRepository, PublishStatusRepository>();
builder.Services.AddScoped<IAppRoleRepository, AppRoleRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<ICourseGroupRepository, CourseGroupRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1"));

app.UseCors(LocalhostCorsPolicy);

app.MapControllers();

app.Run();

// Exposed so the test project can reference the entry-point assembly.
public partial class Program;
