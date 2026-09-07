using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Infrastructure;

/// <summary>
/// SHA-256 of the UTF-8 bytes, rendered as lowercase hex (64 chars) — the format stored in <c>AppUser.PasswordHash</c>.
/// </summary>
public static class PasswordHasher
{
    public static string Sha256Hex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
