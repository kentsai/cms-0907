using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IRowAuditRepository
{
    /// <summary>Audit rows for one record, newest first, capped at <paramref name="take"/>.</summary>
    Task<IReadOnlyList<RowAudit>> GetForRowAsync(string tableName, string primaryKeyValues, int take, CancellationToken cancellationToken);
}
