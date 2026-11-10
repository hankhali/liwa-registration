using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Design.Internal;

namespace Liwa.UnitTests.Tools;

/// <summary>
/// Adds an EF Core migration without the dotnet-ef CLI (installing CLI tools is not allowed on the
/// development machine). Runs only when LIWA_ADD_MIGRATION is set; otherwise it does nothing:
///   $env:LIWA_ADD_MIGRATION='CatalogVersions'; dotnet test tests/Liwa.UnitTests --filter FullyQualifiedName~MigrationScaffolderTool
/// Writes the migration, its designer file and the updated model snapshot into
/// src/Liwa.Infrastructure/Persistence/Migrations, exactly like `dotnet ef migrations add`.
/// </summary>
public sealed class MigrationScaffolderTool
{
    [Fact]
    public void Add_migration_when_requested()
    {
        var name = Environment.GetEnvironmentVariable("LIWA_ADD_MIGRATION");
        if (string.IsNullOrWhiteSpace(name)) return;

        var options = new DbContextOptionsBuilder<LiwaDbContext>()
            .UseNpgsql("Host=localhost;Database=liwa_design;Username=design;Password=design")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var context = new LiwaDbContext(options);

        var services = new ServiceCollection();
        services.AddEntityFrameworkDesignTimeServices();
        services.AddDbContextDesignTimeServices(context);
#pragma warning disable EF1001 // Internal EF Core API: the provider's design-time services.
        new NpgsqlDesignTimeServices().ConfigureDesignTimeServices(services);
#pragma warning restore EF1001
        using var provider = services.BuildServiceProvider();

        var scaffolder = provider.GetRequiredService<IMigrationsScaffolder>();
        var migration = scaffolder.ScaffoldMigration(name, "Liwa.Infrastructure", "Persistence.Migrations");

        var projectDir = Path.Combine(FindRepoRoot(), "src", "Liwa.Infrastructure");
        var files = scaffolder.Save(projectDir, migration, Path.Combine(projectDir, "Persistence", "Migrations"));
        Assert.True(File.Exists(files.MigrationFile));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Liwa.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Liwa.slnx) not found.");
    }
}
