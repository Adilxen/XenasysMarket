using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Xenasys.Core.Abstractions;
using Xenasys.Infrastructure;
using Xenasys.Persistence;
using Xenasys.Persistence.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    // Prefer HTTP 422 over default 400 for DataAnnotations / model binding failures.
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value is { Errors.Count: > 0 })
            .ToDictionary(
                kvp => kvp.Key switch
                {
                    "Username" or "username" => "userName",
                    "Password" or "password" => "password",
                    "CompanyId" or "companyId" or "company_id" => TenancyKeyNames.CompanyId,
                    var key => key.Length > 0
                        ? char.ToLowerInvariant(key[0]) + key[1..]
                        : key,
                },
                kvp => kvp.Value!.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? "Invalid value."
                        : e.ErrorMessage)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return new UnprocessableEntityObjectResult(new { errors });
    };
});

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                ?? ["http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddInfrastructure();
builder.Services.AddAuthRepositories();

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing from configuration.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        defaultConnection,
        sql =>
        {
            sql.MigrationsAssembly("Xenasys.Persistence");
            sql.CommandTimeout(30);
            sql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });

    // SQL-first migrations (T5.1 Stock) may precede ModelSnapshot sync — do not block startup.
    options.ConfigureWarnings(w =>
        w.Ignore(RelationalEventId.PendingModelChangesWarning));

    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "Xenasys_Dev_Only_Replace_With_256bit_Secret_Key!!";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep short claim names (role / tenant_id / user_id). Remapping breaks RequireRole
        // when RoleClaimType is "role" but inbound map rewrites it to ClaimTypes.Role.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Xenasys",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "Xenasys.Frontend",
            IssuerSigningKey = signingKey,
            NameClaimType = TenancyKeyNames.JwtUserId,
            RoleClaimType = TenancyKeyNames.JwtRole,
        };
    });

builder.Services.AddAuthorization();
// Named RBAC policies registered inside AddInfrastructure → AddXenasysRbacAuthorization
builder.Services.AddSingleton<Xenasys.Presentation.Storage.CatalogImageStorage>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Local FE uses http://localhost:5077 — HTTPS redirect breaks browser CORS/SSL.
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseStaticFiles();
app.UseAuthentication();
app.UseTenantResolution();
app.UseTenantClaimGuard();
app.UseAuthorization();
app.MapControllers();

app.Run();
