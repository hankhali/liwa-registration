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
    /// <summary>Refuses to start with the mock AI provider outside Development (brief §4). Call after the database is ready.</summary>
    public static async Task EnsureAiProviderAllowedAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var provider = await scope.ServiceProvider.GetRequiredService<Application.Admin.ISettingsService>().GetAiProviderAsync(ct);
        Explainer.ExplainerService.EnsureProviderAllowed(provider, scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>());
    }

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
        // SIS providers (brief §6): "Sis:Provider" chooses the backend; nothing else knows which one is in use.
        var sis = config.GetValue("Sis:Provider", Application.Sis.SisProviderKind.Database);
        if (sis == Application.Sis.SisProviderKind.PeopleSoft) services.AddScoped<Sis.PeopleSoftSisProvider>();
        else services.AddScoped<Sis.DatabaseSisProvider>();
        Type sisType = sis == Application.Sis.SisProviderKind.PeopleSoft ? typeof(Sis.PeopleSoftSisProvider) : typeof(Sis.DatabaseSisProvider);
        services.AddScoped(typeof(Application.Sis.IStudentRecordProvider), sp => sp.GetRequiredService(sisType));
        services.AddScoped(typeof(Application.Sis.ICourseCatalogProvider), sp => sp.GetRequiredService(sisType));
        services.AddScoped(typeof(Application.Sis.ITermScheduleProvider), sp => sp.GetRequiredService(sisType));

        services.AddSingleton<Security.SecretProtector>();
        services.AddMemoryCache();
        services.AddHttpClient("llm", c => c.Timeout = Timeout.InfiniteTimeSpan); // per-call timeout from the AI settings
        services.AddSingleton<Explainer.ILlmClientFactory, Explainer.LlmClientFactory>();
        services.AddScoped<Explainer.ExplainerService>();
        services.AddScoped<Application.Explainer.IExplainerService>(sp => sp.GetRequiredService<Explainer.ExplainerService>());
        services.AddHttpClient("connection-test");
        services.AddScoped<Application.Admin.IAdminCatalogService, Admin.AdminCatalogService>();
        services.AddScoped<Application.Admin.ICreditPolicyAdmin, Admin.CreditPolicyAdmin>();
        services.AddScoped<Application.Admin.IStaffAdmin, Admin.StaffAdmin>();
        services.AddScoped<Application.Admin.ISettingsService, Admin.SettingsService>();
        services.AddScoped<StudentPreferencesService>();
        services.AddScoped<IStudentPreferencesService>(sp => sp.GetRequiredService<StudentPreferencesService>());
        services.AddScoped<IUserSettingsService>(sp => sp.GetRequiredService<StudentPreferencesService>());
        services.AddScoped<INotificationService, NotificationService>();

        services.AddHealthChecks().AddDbContextCheck<LiwaDbContext>("database", tags: ["ready"]);
        return services;
    }
}
