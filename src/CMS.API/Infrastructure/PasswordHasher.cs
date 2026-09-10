using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Infrastructure;

/// <summary>
/// How <c>AppUser.PasswordHash</c> is produced and checked.
/// <para>
/// New hashes are <b>PBKDF2-HMAC-SHA256</b> over a per-user random salt, stored as the self-describing string
/// <c>pbkdf2-sha256$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 hash&gt;</c> (about 90 characters, well inside the
/// <c>nvarchar(800)</c> column). The salt makes two users who picked the same password store different values,
/// and the iteration count makes an offline guess expensive instead of free.
/// </para>
/// <para>
/// Rows written before this change hold a bare unsalted SHA-256 hex digest (64 lowercase hex characters), the
/// legacy format inherited from the previous system. <see cref="Verify"/> still accepts those so nobody is locked
/// out, and reports them through its <c>needsUpgrade</c> flag; <c>AuthController.Login</c> then rewrites the row
/// in the new format using the plaintext it just verified. The migration therefore happens silently, one user at
/// a time, on their next sign-in — no forced reset, no downtime. <see cref="Sha256Hex"/> exists only to serve that
/// legacy path and must never be used to store a new password.
/// </para>
/// </summary>
public static class PasswordHasher
{
    /// <summary>Marks a stored value as the PBKDF2 format; the first field of <see cref="Hash"/>'s output.</summary>
    public const string Pbkdf2Prefix = "pbkdf2-sha256";

    /// <summary>
    /// Iteration count written into every new hash. Raising it is safe and self-migrating: <see cref="Verify"/>
    /// keeps accepting hashes stored with a lower count and flags them for re-hashing on the next sign-in.
    /// </summary>
    public const int Iterations = 210_000;

    /// <summary>Bytes of cryptographically random salt per password.</summary>
    public const int SaltBytes = 16;

    /// <summary>Bytes of derived key material stored per password.</summary>
    public const int HashBytes = 32;

    /// <summary>Length of a legacy unsalted SHA-256 hex digest.</summary>
    public const int LegacyHexLength = 64;

    private const char FieldSeparator = '$';
    private const int FieldCount = 4;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// Hashes <paramref name="password"/> in the current format with a fresh random salt. Two calls with the same
    /// input return different strings by design — compare with <see cref="Verify"/>, never with string equality.
    /// </summary>
    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashBytes);

        return string.Join(FieldSeparator, Pbkdf2Prefix, Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    /// <summary>
    /// True when <paramref name="password"/> matches <paramref name="storedHash"/>, in either the current or the
    /// legacy format. <paramref name="needsUpgrade"/> is true only on a match that should be rewritten by
    /// <see cref="Hash"/>: a legacy digest, or a PBKDF2 hash below the current <see cref="Iterations"/>. A missing,
    /// blank or malformed stored value is simply a non-match — it never throws, so a corrupt row cannot 500 a login.
    /// </summary>
    public static bool Verify(string password, string? storedHash, out bool needsUpgrade)
    {
        ArgumentNullException.ThrowIfNull(password);
        needsUpgrade = false;

        var stored = storedHash?.Trim();
        if (string.IsNullOrEmpty(stored))
        {
            return false;
        }

        if (IsLegacyFormat(stored))
        {
            // Legacy rows were compared case-insensitively, so keep doing that; the comparison itself stays constant-time.
            var matched = FixedTimeEquals(Sha256Hex(password), stored.ToLowerInvariant());
            needsUpgrade = matched;
            return matched;
        }

        return VerifyPbkdf2(password, stored, out needsUpgrade);
    }

    /// <summary>True for a bare unsalted SHA-256 hex digest — the format used before PBKDF2 was introduced.</summary>
    public static bool IsLegacyFormat(string? storedHash)
    {
        var stored = storedHash?.Trim();
        return stored is { Length: LegacyHexLength } && stored.All(char.IsAsciiHexDigit);
    }

    /// <summary>
    /// SHA-256 of the UTF-8 bytes as lowercase hex. <b>Legacy only</b>: it is unsalted and single-round, so it is
    /// used to read rows written by the previous system and never to store a new password (use <see cref="Hash"/>).
    /// </summary>
    public static string Sha256Hex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static bool VerifyPbkdf2(string password, string stored, out bool needsUpgrade)
    {
        needsUpgrade = false;

        var fields = stored.Split(FieldSeparator);
        if (fields.Length != FieldCount || !string.Equals(fields[0], Pbkdf2Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations <= 0)
        {
            return false;
        }

        if (!TryFromBase64(fields[2], out var salt) || !TryFromBase64(fields[3], out var expected) || expected.Length == 0)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return false;
        }

        needsUpgrade = iterations < Iterations;
        return true;
    }

    private static bool TryFromBase64(string value, out byte[] bytes)
    {
        var buffer = new byte[((value.Length + 3) / 4) * 3];
        if (Convert.TryFromBase64String(value, buffer, out var written))
        {
            bytes = buffer[..written];
            return true;
        }

        bytes = [];
        return false;
    }

    /// <summary>Length-checked constant-time comparison of two ASCII strings.</summary>
    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
