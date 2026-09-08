using CMS.API.Infrastructure;

namespace CMS.API.Tests.Infrastructure;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdefg1")]   // upper + lower + digit, exactly MinLength
    [InlineData("abcdefg1!")]  // lower + digit + symbol
    [InlineData("ABCDEFG1!")]  // upper + digit + symbol
    [InlineData("Abcdefg!")]   // upper + lower + symbol
    [InlineData("Ab1!Ab1!")]   // all four
    [InlineData("Ab1~Ab1~")]   // symbols at the top of the printable range
    [InlineData("Ab1 Ab1 ")]   // spaces allowed, still 3 classes
    public void IsCompliant_AcceptsLength8PlusWithThreeClasses(string password)
    {
        Assert.True(PasswordPolicy.IsCompliant(password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ab1!")]       // too short
    [InlineData("Abcdef1")]    // 7 characters
    [InlineData("abcdefgh")]   // 1 class
    [InlineData("abcdefg1")]   // 2 classes
    [InlineData("Abcdefgh")]   // 2 classes
    [InlineData("ABCDEFG1")]   // 2 classes
    [InlineData("1234567!")]   // 2 classes
    [InlineData("abcd 123")]   // whitespace is not a symbol
    [InlineData("abcd中文123")] // CJK is not a symbol
    [InlineData("ａｂｃＡＢＣ１２３")] // full-width letters/digits are not ASCII classes
    public void IsCompliant_RejectsShortOrLowVarietyPasswords(string? password)
    {
        Assert.False(PasswordPolicy.IsCompliant(password));
    }

    [Fact]
    public void Message_IsTheAgreedBilingualRuleText()
    {
        Assert.Equal("密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號", PasswordPolicy.Message);
    }
}
