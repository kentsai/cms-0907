using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IPublishStatusRepository
{
    Task<IReadOnlyList<PublishStatus>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken);
    Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(byte pkid, CancellationToken cancellationToken);

    /// <summary>Inserts the row and returns its (caller-assigned) pkid.</summary>
    Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken);

    /// <summary>Returns false when no row matched <see cref="PublishStatusRequest.Pkid"/>.</summary>
    Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns false when no row matched. Throws <see cref="Infrastructure.EntityInUseException"/> when the row
    /// is still referenced by a foreign key (e.g. Course, Promotion2).
    /// </summary>
    Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken);

    /// <summary>Slim list for FK dropdowns, ordered by pkid.</summary>
    Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken);
}
