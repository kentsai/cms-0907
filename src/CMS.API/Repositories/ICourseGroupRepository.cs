using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICourseGroupRepository
{
    Task<IReadOnlyList<CourseGroup>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken cancellationToken);
    Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken);

    /// <summary>Inserts the row and returns the new IDENTITY pkid.</summary>
    Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken);

    /// <summary>Returns false when no row matched <see cref="CourseGroupRequest.Pkid"/>.</summary>
    Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns false when no row matched. Throws <see cref="Infrastructure.EntityInUseException"/> when the row
    /// is still referenced by a foreign key (Course, PartnerCourseGroup).
    /// </summary>
    Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken);

    /// <summary>Slim list for FK dropdowns, ordered by Description.</summary>
    Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken);
}
