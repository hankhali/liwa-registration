using Liwa.Application.Abstractions;
using Liwa.Domain.Activity;
using Liwa.Domain.Users;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liwa.Infrastructure.Seeding;

/// <summary>
/// Creates the first administrator on a fresh server (no demo data):
/// <c>dotnet Liwa.Api.dll create-admin &lt;username&gt; "&lt;Display Name&gt;"</c> with the password in LIWA_ADMIN_PASSWORD.
/// Refuses if the username exists; the password never appears on the command line.
/// </summary>
public static class AdminBootstrap
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output)
    {
        if (args.Length < 3)
        {
            await output.WriteLineAsync("Usage: dotnet Liwa.Api.dll create-admin <username> \"<Display Name>\"   (password in LIWA_ADMIN_PASSWORD)");
            return 2;
        }

        var password = Environment.GetEnvironmentVariable("LIWA_ADMIN_PASSWORD") ?? "";
        if (password.Length < 12)
        {
            await output.WriteLineAsync("Set LIWA_ADMIN_PASSWORD to a password of at least 12 characters.");
            return 2;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiwaDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var username = User.NormalizeUsername(args[1]);
        if (await db.Users.AnyAsync(u => u.Username == username))
        {
            await output.WriteLineAsync($"The username {username} already exists; nothing changed.");
            return 1;
        }

        var user = new User(Guid.NewGuid(), username, args[2], null, UserRole.Admin, null, clock.GetUtcNow());
        user.SetPasswordHash(hasher.Hash(password));
        db.Users.Add(user);
        db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), null, "user.created", "User", user.Id.ToString(), null, "{\"role\":\"Admin\",\"via\":\"create-admin\"}"));
        await db.SaveChangesAsync();
        await output.WriteLineAsync($"Administrator {username} created. Sign in and add the other staff accounts on the Users page.");
        return 0;
    }
}
