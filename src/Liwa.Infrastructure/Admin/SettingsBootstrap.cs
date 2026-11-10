using Liwa.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Liwa.Infrastructure.Admin;

/// <summary>
/// Applies the sign-in settings an administrator saved in the app (app_settings) on top of the configuration when the
/// API starts, so the OpenID Connect handler is built with them. Read once, before the app is built; if the table
/// doesn't exist yet (first start) or the database is down, configuration stays as it is.
/// </summary>
public static class SettingsBootstrap
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        ["sso.enabled"] = "Sso:Enabled",
        ["sso.tenantId"] = "Sso:TenantId",
        ["sso.clientId"] = "Sso:ClientId",
        ["sso.clientSecret"] = "Sso:ClientSecret",
        ["sso.localSignIn"] = "Sso:LocalSignInEnabled",
    };

    public static IDictionary<string, string?> Load(IConfiguration config)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        var cs = config.GetConnectionString("Liwa");
        if (string.IsNullOrWhiteSpace(cs)) return result;
        try
        {
            var protector = new SecretProtector(config);
            using var conn = new NpgsqlConnection(cs);
            conn.Open();
            using var exists = new NpgsqlCommand("SELECT to_regclass('public.app_settings') IS NOT NULL", conn);
            if (exists.ExecuteScalar() is not true) return result;
            using var cmd = new NpgsqlCommand("SELECT key, value, is_secret FROM app_settings WHERE key LIKE 'sso.%'", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(0);
                if (!Map.TryGetValue(key, out var target) || reader.IsDBNull(1)) continue;
                var value = reader.GetString(1);
                result[target] = reader.GetBoolean(2) ? protector.Unprotect(value) : value;
            }
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or System.Security.Cryptography.CryptographicException or FormatException)
        {
            // Keep the file/environment configuration.
        }

        return result;
    }
}
