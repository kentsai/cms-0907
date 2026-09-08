using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IRowAuditRepository
{
    /// <summary>
    /// The full audit trail of one record — every <c>dbo.RowAudit</c> row whose <c>TableName</c> is
    /// <paramref name="tableName"/> and whose <c>PrimaryKeyValues</c> is <paramref name="pkid"/> — newest first.
    /// </summary>
    Task<IReadOnlyList<RowAudit>> GetForRecordAsync(string tableName, string pkid, CancellationToken cancellationToken);
}
