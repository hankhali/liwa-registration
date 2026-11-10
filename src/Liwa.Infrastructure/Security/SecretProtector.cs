using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Liwa.Infrastructure.Security;

/// <summary>
/// Encrypts secrets at rest (SSO client secret, AI API key) with AES-256-GCM (brief §4). The key comes from
/// configuration <c>Secrets:Key</c> (base64, 32 bytes); without it, a key is derived from <c>Jwt:SigningKey</c>
/// so development works out of the box. Production should set its own <c>Secrets:Key</c> (docs/INSTALL.md).
/// </summary>
public sealed class SecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public SecretProtector(IConfiguration config)
    {
        var configured = config["Secrets:Key"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _key = Convert.FromBase64String(configured);
            if (_key.Length != 32) throw new InvalidOperationException("Secrets:Key must be 32 bytes, base64-encoded.");
        }
        else
        {
            var jwt = config["Jwt:SigningKey"] ?? throw new InvalidOperationException("Set Secrets:Key (or Jwt:SigningKey) to encrypt stored secrets.");
            _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwt), 32, info: Encoding.UTF8.GetBytes("liwa-secrets-v1"));
        }
    }

    public string Protect(string plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var data = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[data.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(_key, TagSize)) aes.Encrypt(nonce, data, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Unprotect(string protectedValue)
    {
        var all = Convert.FromBase64String(protectedValue);
        var nonce = all.AsSpan(0, NonceSize);
        var tag = all.AsSpan(NonceSize, TagSize);
        var cipher = all.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(_key, TagSize)) aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>"••••abcd": the only form a secret is ever shown in.</summary>
    public static string Mask(string? plain) => string.IsNullOrEmpty(plain) ? "" : "••••" + (plain.Length <= 4 ? plain : plain[^4..]);
}
