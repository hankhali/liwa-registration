using Liwa.Application.Abstractions;
using Liwa.Application.Academic;
using Liwa.Application.Imports;
using Liwa.Application.Planning;
using Liwa.Infrastructure.Academic;
using Liwa.Infrastructure.Imports;
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
        services.AddOptions<CreditPolicyDefaults>().Bind(config.GetSection(CreditPolicyDefaults.Section));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<ISignInHandoff, InMemorySignInHandoff>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<DemoSeeder>();
        services.AddScoped<CreditPolicySeeder>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IAcademicQueries, AcademicQueries>();
        services.AddScoped<StudentPlanService>();
        services.AddScoped<IStudentPlanService>(sp => sp.GetRequiredService<StudentPlanService>());
        services.AddScoped<IRecommendationService>(sp => sp.GetRequiredService<StudentPlanService>());
        services.AddScoped<IAdvisorService>(sp => sp.GetRequiredService<StudentPlanService>());
        services.AddScoped<IPlanRevalidationService>(sp => sp.GetRequiredService<StudentPlanService>());
        services.AddScoped<Notifier>();
        services.AddScoped<StudentPreferencesService>();
        services.AddScoped<IStudentPreferencesService>(sp => sp.GetRequiredService<StudentPreferencesService>());
        services.AddScoped<IUserSettingsService>(sp => sp.GetRequiredService<StudentPreferencesService>());
        services.AddScoped<INotificationService, NotificationService>();

        services.AddHealthChecks().AddDbContextCheck<LiwaDbContext>("database", tags: ["ready"]);
        return services;
    }
}
