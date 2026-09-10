namespace CMS.API.Infrastructure;

/// <summary>
/// The SQL Server error numbers the repositories translate into domain exceptions. They were previously
/// re-declared as a private constant in each of the eight repositories, which is how one of them ended up
/// catching a different set from its siblings.
/// </summary>
public static class SqlErrorNumbers
{
    /// <summary>A DELETE (or an INSERT/UPDATE) rejected by a foreign-key constraint. → <see cref="EntityInUseException"/>.</summary>
    public const int ForeignKeyViolation = 547;

    /// <summary>A PRIMARY KEY or UNIQUE *constraint* violation. → <see cref="DuplicateKeyException"/> / <see cref="SlotOccupiedException"/>.</summary>
    public const int UniqueConstraintViolation = 2627;

    /// <summary>A UNIQUE *index* violation — the same condition, reported when the index is not backed by a constraint.</summary>
    public const int UniqueIndexViolation = 2601;

    /// <summary>True when the exception is either form of "this key already exists".</summary>
    public static bool IsUniqueViolation(int number) =>
        number is UniqueConstraintViolation or UniqueIndexViolation;
}
