using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Liwa.Application.Abstractions;
using Liwa.Application.Auth;

namespace Liwa.Infrastructure.Security;

/// <summary>
/// Single-use, 60-second sign-in codes held in memory. Only a SHA-256 hash of each code is stored.
/// Fine for one API instance; with several instances behind a load balancer, replace with a shared
/// store (e.g. Redis or a database table) — see docs/sso.md.
/// </summary>
public sealed class InMemorySignInHandoff(TimeProvider clock) : ISignInHandoff
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, (SignInSuccess SignIn, DateTimeOffset ExpiresAt)> _entries = new();

    public string Create(SignInSuccess signIn)
    {
        RemoveExpired();
        var code = Base64Url(RandomNumberGenerator.GetBytes(32));
        _entries[Hash(code)] = (signIn, clock.GetUtcNow() + Lifetime);
        return code;
    }

    public SignInSuccess? Redeem(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 100) return null;
        if (!_entries.TryRemove(Hash(code), out var entry)) return null;
        return entry.ExpiresAt > clock.GetUtcNow() ? entry.SignIn : null;
    }

    private void RemoveExpired()
    {
        var now = clock.GetUtcNow();
        foreach (var (key, value) in _entries)
            if (value.ExpiresAt <= now) _entries.TryRemove(key, out _);
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
