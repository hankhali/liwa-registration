using System.Reflection;
using System.Text.Json;
using System.Threading.RateLimiting;
using Liwa.Application.Abstractions;
using Liwa.Domain.Users;
using Liwa.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Liwa.Api.Infrastructure;

public static class Policies
{
    public const string Student = "Student";
    public const string Advisor = "Advisor";
    public const string Admin = "Admin";
    /// <summary>Advisors and admins.</summary>
    public const string Staff = "Staff";
}

public static class RateLimits
{
    public const string SignIn = "sign-in";
}

public static class ApiSetup
{
    public static IServiceCollection AddLiwaApi(this IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
    {
        services.AddControllers();
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<UnhandledExceptionHandler>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var o = jwt.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = o.Issuer,
                    ValidAudience = o.Audience,
                    IssuerSigningKey = o.CreateKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = LiwaClaims.Name,
                    RoleClaimType = LiwaClaims.Role,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Student, p => p.RequireRole(RoleNames.Student))
            .AddPolicy(Policies.Advisor, p => p.RequireRole(RoleNames.Advisor))
            .AddPolicy(Policies.Admin, p => p.RequireRole(RoleNames.Admin))
            .AddPolicy(Policies.Staff, p => p.RequireRole(RoleNames.Advisor, RoleNames.Admin))
            // Everything requires a signed-in user unless marked [AllowAnonymous].
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        var permitPerMinute = config.GetValue("RateLimiting:SignInPermitsPerMinute", 10);
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(RateLimits.SignIn, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // The API is only reachable through the nginx container on a private network.
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        if (config.GetValue("Swagger:Enabled", env.IsDevelopment()))
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(o =>
            {
                o.SwaggerDoc("v1", new OpenApiInfo { Title = "Liwa Registration API", Version = "v1" });
                o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Paste the accessToken from POST /api/auth/login",
                });
                o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", doc)] = [] });
            });
        }

        return services;
    }

    public static WebApplication UseLiwaApi(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
        {
            app.UseSwagger(o => o.RouteTemplate = "api/swagger/{documentName}/swagger.json");
            app.UseSwaggerUI(o =>
            {
                o.RoutePrefix = "api/swagger";
                o.SwaggerEndpoint("v1/swagger.json", "Liwa Registration API v1");
            });
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        // Liveness: the process is up. Readiness: it can reach the database.
        app.MapHealthChecks("/api/health", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteHealth }).AllowAnonymous();
        app.MapHealthChecks("/api/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready"), ResponseWriter = WriteHealth }).AllowAnonymous();

        app.MapControllers();
        return app;
    }

    public static string Version { get; } =
        typeof(ApiSetup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "dev";

    private static Task WriteHealth(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            version = Version,
            checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString()),
        }));
    }
}
