using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Slim lookups for tables that do not (yet) have their own repository. Once a table gets a full
/// repository, prefer exposing the lookup there and pointing <c>LookupsController</c> at it
/// (as happened with AppUser, Partner, CourseGroup and Course).
/// </summary>
public interface ILookupRepository
{
    /// <summary>All certifications: label = "Partner.Name Title" (Title is nchar → RTRIM), ordered by partner then title.</summary>
    Task<IReadOnlyList<LookupItem>> GetCertificationsAsync(CancellationToken cancellationToken);

    /// <summary>All job categories: label = Description, ordered by Description.</summary>
    Task<IReadOnlyList<LookupItem>> GetJobCategoriesAsync(CancellationToken cancellationToken);
}
