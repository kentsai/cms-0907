using System.Data.Common;
using Dapper;

namespace CMS.API.Infrastructure;

public interface IRowAuditWriter
{
    /// <summary>
    /// Inserts one <c>dbo.RowAudit</c> row on the caller's open connection (and transaction, when supplied)
    /// so the audit is committed together with the data change.
    /// </summary>
    Task WriteAsync(
        DbConnection connection,
        string tableName,
        string primaryKeyValues,
        string actionType,
        string? actionDesc,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null);
}

public sealed class RowAuditWriter(IHttpContextAccessor httpContextAccessor) : IRowAuditWriter
{
    public const string Insert = "INSERT";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";

    private const string Sql = """
        INSERT INTO RowAudit (TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime])
        VALUES (@TableName, @UserName, @PrimaryKeyValues, @ActionType, @ActionDesc, GETUTCDATE());
        """;

    public Task WriteAsync(
        DbConnection connection,
        string tableName,
        string primaryKeyValues,
        string actionType,
        string? actionDesc,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        var userName = httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            // No authentication in this scaffold yet.
            userName = "system";
        }

        var parameters = new
        {
            TableName = Truncate(tableName, 50),
            UserName = Truncate(userName, 100),
            PrimaryKeyValues = Truncate(primaryKeyValues, 100),
            ActionType = Truncate(actionType, 20),
            ActionDesc = actionDesc is null ? null : Truncate(actionDesc, 1000)
        };

        return connection.ExecuteAsync(new CommandDefinition(Sql, parameters, transaction, cancellationToken: cancellationToken));
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
