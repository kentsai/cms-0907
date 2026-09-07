using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Slim lookups for tables that do not (yet) have their own repository. Once a table gets a full
/// repository, prefer exposing the lookup there and pointing <c>LookupsController</c> at it.
/// </summary>
public interface ILookupRepository
{
    /// <summary>All users: id = UserId, label = "UserName (UserId)", ordered by UserName.</summary>
    Task<IReadOnlyList<StringLookupItem>> GetAppUsersAsync(CancellationToken cancellationToken);
}
