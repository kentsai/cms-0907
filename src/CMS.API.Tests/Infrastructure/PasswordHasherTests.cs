using CMS.API.Infrastructure;

namespace CMS.API.Tests.Infrastructure;

public class PasswordHasherTests
{
    private const string Password = "P@ssw0rd!";

    // ---- current format: salted PBKDF2 ----

    [Fact]
    public void Hash_ProducesTheSelfDescribingPbkdf2Format()
    {
        var hash = PasswordHasher.Hash(Password);

        var fields = hash.Split('$');
        Assert.Equal(4, fields.Length);
        Assert.Equal(PasswordHasher.Pbkdf2Prefix, fields[0]);
        Assert.Equal(PasswordHasher.Iterations.ToString(), fields[1]);
        Assert.Equal(PasswordHasher.SaltBytes, Convert.FromBase64String(fields[2]).Length);
        Assert.Equal(PasswordHasher.HashBytes, Convert.FromBase64String(fields[3]).Length);
    }

    [Fact]
    public void Hash_FitsThePasswordHashColumn()
    {
        // AppUser.PasswordHash is nvarchar(800).
        Assert.True(PasswordHasher.Hash(Password).Length <= 800);
    }

    [Fact]
    public void Hash_SaltsEveryCall_SoTheSamePasswordNeverStoresTheSameValue()
    {
        var first = PasswordHasher.Hash(Password);
        var second = PasswordHasher.Hash(Password);

        Assert.NotEqual(first, second);
        // Both still verify: the salt travels with the hash.
        Assert.True(PasswordHasher.Verify(Password, first, out _));
        Assert.True(PasswordHasher.Verify(Password, second, out _));
    }

    [Fact]
    public void Verify_AcceptsTheRightPassword_AndFlagsNoUpgrade()
    {
        Assert.True(PasswordHasher.Verify(Password, PasswordHasher.Hash(Password), out var needsUpgrade));
        Assert.False(needsUpgrade);
    }

    [Theory]
    [InlineData("p@ssw0rd!")]      // case differs
    [InlineData("P@ssw0rd")]       // one character short
    [InlineData("P@ssw0rd!!")]     // one character extra
    [InlineData("")]
    public void Verify_RejectsAnythingButTheRightPassword(string candidate)
    {
        Assert.False(PasswordHasher.Verify(candidate, PasswordHasher.Hash(Password), out var needsUpgrade));
        Assert.False(needsUpgrade);
    }

    [Fact]
    public void Verify_UsesUtf8_SoNonAsciiPasswordsRoundTrip()
    {
        var hash = PasswordHasher.Hash("密碼Aa1!");

        Assert.True(PasswordHasher.Verify("密碼Aa1!", hash, out _));
        Assert.False(PasswordHasher.Verify("密码Aa1!", hash, out _));
    }

    [Fact]
    public void Verify_FlagsAnUpgrade_WhenTheStoredIterationCountIsBelowTheCurrentOne()
    {
        var salt = Convert.ToBase64String(new byte[PasswordHasher.SaltBytes]);
        var weaker = PasswordHasher.Iterations / 2;
        var key = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            Password, new byte[PasswordHasher.SaltBytes], weaker,
            System.Security.Cryptography.HashAlgorithmName.SHA256, PasswordHasher.HashBytes);
        var stored = $"{PasswordHasher.Pbkdf2Prefix}${weaker}${salt}${Convert.ToBase64String(key)}";

        Assert.True(PasswordHasher.Verify(Password, stored, out var needsUpgrade));
        Assert.True(needsUpgrade);
    }

    // ---- legacy format: unsalted SHA-256 hex ----

    [Fact]
    public void Verify_AcceptsALegacyHexDigest_AndFlagsItForUpgrade()
    {
        Assert.True(PasswordHasher.Verify(Password, PasswordHasher.Sha256Hex(Password), out var needsUpgrade));
        Assert.True(needsUpgrade);
    }

    [Fact]
    public void Verify_AcceptsALegacyHexDigest_WhateverItsCaseAndPadding()
    {
        var stored = $"  {PasswordHasher.Sha256Hex(Password).ToUpperInvariant()}  ";

        Assert.True(PasswordHasher.Verify(Password, stored, out var needsUpgrade));
        Assert.True(needsUpgrade);
    }

    [Fact]
    public void Verify_RejectsAWrongPasswordAgainstALegacyDigest_AndFlagsNoUpgrade()
    {
        Assert.False(PasswordHasher.Verify("wrong", PasswordHasher.Sha256Hex(Password), out var needsUpgrade));
        Assert.False(needsUpgrade);
    }

    [Fact]
    public void IsLegacyFormat_SeparatesTheTwoStoredShapes()
    {
        Assert.True(PasswordHasher.IsLegacyFormat(PasswordHasher.Sha256Hex(Password)));
        Assert.True(PasswordHasher.IsLegacyFormat(PasswordHasher.Sha256Hex(Password).ToUpperInvariant()));
        Assert.False(PasswordHasher.IsLegacyFormat(PasswordHasher.Hash(Password)));
        Assert.False(PasswordHasher.IsLegacyFormat(null));
        Assert.False(PasswordHasher.IsLegacyFormat(""));
        Assert.False(PasswordHasher.IsLegacyFormat(new string('z', PasswordHasher.LegacyHexLength)));
    }

    // ---- a corrupt or missing stored value is a non-match, never an exception ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-hash")]
    [InlineData("pbkdf2-sha256$")]
    [InlineData("pbkdf2-sha256$210000$onlythreefields")]
    [InlineData("pbkdf2-sha256$notanumber$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$0$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$-1$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$210000$not base64$AAAA")]
    [InlineData("pbkdf2-sha256$210000$AAAA$not base64")]
    [InlineData("bcrypt$210000$AAAA$AAAA")]
    public void Verify_ReturnsFalse_ForAMissingOrMalformedStoredHash(string? stored)
    {
        Assert.False(PasswordHasher.Verify(Password, stored, out var needsUpgrade));
        Assert.False(needsUpgrade);
    }

    [Fact]
    public void Hash_And_Verify_Throw_OnANullPassword()
    {
        Assert.Throws<ArgumentNullException>(() => PasswordHasher.Hash(null!));
        Assert.Throws<ArgumentNullException>(() => PasswordHasher.Verify(null!, PasswordHasher.Hash(Password), out _));
    }

    // ---- Sha256Hex itself (legacy verification only) ----

    [Fact]
    public void Sha256Hex_MatchesKnownVector_InLowercaseHex()
    {
        // NIST test vector for "abc".
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", PasswordHasher.Sha256Hex("abc"));
    }

    [Fact]
    public void Sha256Hex_Is64Chars_AndDeterministic()
    {
        var first = PasswordHasher.Sha256Hex(Password);
        var second = PasswordHasher.Sha256Hex(Password);

        Assert.Equal(PasswordHasher.LegacyHexLength, first.Length);
        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public void Sha256Hex_UsesUtf8_ForNonAsciiInput()
    {
        // Distinct from the ASCII hash and stable across runs.
        Assert.NotEqual(PasswordHasher.Sha256Hex("密碼"), PasswordHasher.Sha256Hex("??"));
        Assert.Equal(PasswordHasher.Sha256Hex("密碼"), PasswordHasher.Sha256Hex("密碼"));
    }

    [Fact]
    public void Sha256Hex_Throws_OnNull()
    {
        Assert.Throws<ArgumentNullException>(() => PasswordHasher.Sha256Hex(null!));
    }
}
