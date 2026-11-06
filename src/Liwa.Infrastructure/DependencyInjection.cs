using Liwa.Application.Abstractions;
using Liwa.Infrastructure.Persistence;
using Liwa.Infrastructure.Security;
using Liwa.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Liwa.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Liwa")
            ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Liwa' is not configured.");

        services.AddDbContext<LiwaDbContext>(o => o
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
            .UseSnakeCaseNamingConvention());

        services.AddOptions<JwtOptions>().Bind(config.GetSection(JwtOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<PasswordHashingOptions>().Bind(config.GetSection(PasswordHashingOptions.Section));
        services.AddOptions<DemoSeedOptions>().Bind(config.GetSection(DemoSeedOptions.Section));
        services.AddOptions<DatabaseOptions>().Bind(config.GetSection(DatabaseOptions.Section));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<DemoSeeder>();

        services.AddHealthChecks().AddDbContextCheck<LiwaDbContext>("database", tags: ["ready"]);
        return services;
    }
}
