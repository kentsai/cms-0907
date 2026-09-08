using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICertificationRepository
{
    Task<IReadOnlyList<Certification>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Certification>> QueryAsync(CertificationQuery query, CancellationToken cancellationToken);

    /// <summary>Returns the row with its <c>CoursePkids</c> / <c>JobCategoryPkids</c> populated.</summary>
    Task<Certification?> GetByIdAsync(int pkid, CancellationToken cancellationToken);

    /// <summary>Inserts the row and its junction rows; returns the new IDENTITY pkid.</summary>
    Task<int> CreateAsync(CertificationRequest request, CancellationToken cancellationToken);

    /// <summary>Returns false when no row matched <see cref="CertificationRequest.Pkid"/>.</summary>
    Task<bool> UpdateAsync(CertificationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the row together with its <c>CourseInCertification</c> / <c>CertificationJobCategories</c> links.
    /// Returns false when no row matched. Throws <see cref="Infrastructure.EntityInUseException"/> on a
    /// residual FK violation.
    /// </summary>
    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken);

    /// <summary>Slim list for FK dropdowns: label = "Partner.Name Title", ordered by partner then title.</summary>
    Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken);
}
