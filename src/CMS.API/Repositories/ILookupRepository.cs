using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Slim lookups for tables that do not (yet) have their own repository. Once a table gets a full
/// repository, prefer exposing the lookup there and pointing <c>LookupsController</c> at it
/// (as happened with AppUser, Partner, CourseGroup, Course and Certification).
/// </summary>
public interface ILookupRepository
{
    /// <summary>All job categories: label = Description, ordered by Description.</summary>
    Task<IReadOnlyList<LookupItem>> GetJobCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>All training centres: label = Name, ordered by DisplayOrder then Name.</summary>
    Task<IReadOnlyList<LookupItem>> GetTrainingCentersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Promotions whose PromoCode contains <paramref name="keyword"/> (all when blank), newest code first,
    /// capped at <paramref name="limit"/> rows.
    /// </summary>
    Task<IReadOnlyList<PromotionLookupItem>> SearchPromotionsAsync(string? keyword, int limit, CancellationToken cancellationToken);
}
