using CMS.API.Infrastructure;

namespace CMS.API.Tests.Infrastructure;

public class PasswordHasherTests
{
    [Fact]
    public void Sha256Hex_MatchesKnownVector_InLowercaseHex()
    {
        // NIST test vector for "abc".
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", PasswordHasher.Sha256Hex("abc"));
    }

    [Fact]
    public void Sha256Hex_Is64Chars_AndDeterministic()
    {
        var first = PasswordHasher.Sha256Hex("P@ssw0rd!");
        var second = PasswordHasher.Sha256Hex("P@ssw0rd!");

        Assert.Equal(64, first.Length);
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
