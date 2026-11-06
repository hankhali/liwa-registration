using System.Globalization;
using System.Security.Cryptography;
using Liwa.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Liwa.Infrastructure.Security;

public sealed class PasswordHashingOptions
{
    public const string Section = "Security:PasswordHashing";

    /// <summary>PBKDF2-HMAC-SHA256 iterations. OWASP (2023+) recommends at least 600,000.</summary>
    public int Iterations { get; set; } = 600_000;
}

/// <summary>
/// PBKDF2-HMAC-SHA256 with a random 128-bit salt and 256-bit output.
/// Stored format: <c>PBKDF2-SHA256$iterations$salt-base64$hash-base64</c>. The iteration count is stored
/// per hash, so it can be raised later; old hashes verify and are flagged for rehash.
/// </summary>
public sealed class Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions> options) : IPasswordHasher
{
    private const string Scheme = "PBKDF2-SHA256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    private int Iterations => Math.Max(options.Value.Iterations, 10_000);

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashBytes);
        return string.Join('$', Scheme, Iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public PasswordVerification Verify(string passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (string.IsNullOrEmpty(passwordHash)) return PasswordVerification.Failed;

        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme) return PasswordVerification.Failed;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations <= 0)
            return PasswordVerification.Failed;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return PasswordVerification.Failed;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return PasswordVerification.Failed;

        return iterations < Iterations || expected.Length < HashBytes
            ? PasswordVerification.SuccessRehashNeeded
            : PasswordVerification.Success;
    }
}
