namespace CMS.API.Infrastructure;

/// <summary>
/// Thrown by a repository when a DELETE is rejected by a foreign-key constraint (SQL Server error 547).
/// Controllers translate it to <c>409 Conflict</c>.
/// </summary>
public sealed class EntityInUseException(string message) : Exception(message);
