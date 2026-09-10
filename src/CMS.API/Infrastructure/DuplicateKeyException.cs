namespace CMS.API.Infrastructure;

/// <summary>
/// Thrown by a repository when an INSERT is rejected because the key already exists (SQL Server errors
/// 2627 / 2601). Controllers translate it to <c>409 Conflict</c>.
/// <para>
/// The controllers check <c>ExistsAsync</c> before creating, which answers the ordinary case with a friendly
/// 409. That check is a read followed by a write, though, so two callers racing on the same key both pass it
/// and the second INSERT hits the primary key. The table is never corrupted — the constraint is doing its job
/// — but without this the loser surfaced as a 500 rather than the 409 the endpoint documents.
/// </para>
/// </summary>
public sealed class DuplicateKeyException(string message) : Exception(message);
