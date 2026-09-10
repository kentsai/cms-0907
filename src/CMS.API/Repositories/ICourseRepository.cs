using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICourseRepository
{
    Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Course>> QueryAsync(CourseQuery query, CancellationToken cancellationToken);

    /// <summary>Returns the row with its <c>CertificationPkids</c> / <c>JobCategoryPkids</c> populated.</summary>
    Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken);

    /// <summary>Inserts the row and its junction rows; returns the new IDENTITY pkid.</summary>
    Task<int> CreateAsync(CourseRequest request, CancellationToken cancellationToken);

    /// <summary>Returns false when no row matched <see cref="CourseRequest.Pkid"/>.</summary>
    Task<bool> UpdateAsync(CourseRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns false when no row matched. Throws <see cref="Infrastructure.EntityInUseException"/> when the row
    /// is still referenced by CourseFAQ, CourseRelatedLink or HotCourse (the two N-N junctions cascade).
    /// </summary>
    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken);

    /// <summary>Slim list for FK dropdowns: label = "CourseId Title", ordered by CourseId.</summary>
    Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken);
}
