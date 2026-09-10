namespace CMS.API.Infrastructure;

/// <summary>
/// Complexity rule for a user-chosen password: at least <see cref="MinLength"/> characters and at least
/// <see cref="MinCharacterClasses"/> of the four classes uppercase / lowercase / digit / symbol.
/// Letters and digits are ASCII; a symbol is any other printable ASCII character (<c>!</c>–<c>/</c>,
/// <c>:</c>–<c>@</c>, <c>[</c>–<c>`</c>, <c>{</c>–<c>~</c>). Other characters (whitespace, CJK…) are
/// allowed but count towards no class. The Angular form applies the identical rule
/// (<c>core/utils/password.validator.ts</c>).
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MinCharacterClasses = 3;

    public const string Message =
        "密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號";

    public static bool IsCompliant(string? password)
    {
        if (password is null || password.Length < MinLength)
        {
            return false;
        }

        var upper = false;
        var lower = false;
        var digit = false;
        var symbol = false;
        foreach (var c in password)
        {
            if (char.IsAsciiLetterUpper(c)) upper = true;
            else if (char.IsAsciiLetterLower(c)) lower = true;
            else if (char.IsAsciiDigit(c)) digit = true;
            else if (c is >= '!' and <= '~') symbol = true;
        }

        var classes = (upper ? 1 : 0) + (lower ? 1 : 0) + (digit ? 1 : 0) + (symbol ? 1 : 0);
        return classes >= MinCharacterClasses;
    }
}
