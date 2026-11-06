using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Liwa.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef</c> to build migrations; never at runtime.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LiwaDbContext>
{
    public LiwaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LiwaDbContext>()
            .UseNpgsql("Host=localhost;Database=liwa_design;Username=design;Password=design")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new LiwaDbContext(options);
    }
}
