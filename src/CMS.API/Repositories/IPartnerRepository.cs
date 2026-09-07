using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IPartnerRepository
{
    Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken);
    Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken);

    /// <summary>Inserts the row and returns the new IDENTITY pkid.</summary>
    Task<short> CreateAsync(PartnerRequest request, CancellationToken cancellationToken);

    /// <summary>Returns false when no row matched <see cref="PartnerRequest.Pkid"/>.</summary>
    Task<bool> UpdateAsync(PartnerRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns false when no row matched. Throws <see cref="Infrastructure.EntityInUseException"/> when the row
    /// is still referenced by a foreign key (Course, Certification, PartnerCourseGroup, Seminar, Promotion2).
    /// </summary>
    Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken);

    /// <summary>Slim list for FK dropdowns, ordered by DisplayOrder then Name.</summary>
    Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken);
}
