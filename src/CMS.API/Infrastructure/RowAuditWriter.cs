using System.Data.Common;
using System.Globalization;
using System.Reflection;
using Dapper;

namespace CMS.API.Infrastructure;

/// <summary>
/// Writes one <c>dbo.RowAudit</c> row per data change. Repositories call the generic <c>Log*</c> methods after an
/// INSERT / UPDATE / DELETE on the same open connection (and transaction) so the audit commits with the change.
/// </summary>
public interface IRowAuditWriter
{
    /// <summary>
    /// Inserts one <c>dbo.RowAudit</c> row on the caller's open connection (and transaction, when supplied)
    /// so the audit is committed together with the data change. Low-level form: the caller supplies every column.
    /// </summary>
    Task WriteAsync(
        DbConnection connection,
        string tableName,
        string primaryKeyValues,
        string actionType,
        string? actionDesc,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null);

    /// <summary>
    /// Audits an INSERT of <paramref name="entity"/>: <c>PrimaryKeyValues</c> is its <c>pkid</c> (or the property
    /// marked <see cref="AuditKeyAttribute"/>) and <c>ActionDesc</c> the value of its first string property in
    /// declaration order (a Name / Title / Code field), skipping the key and <see cref="AuditIgnoreAttribute"/> members.
    /// </summary>
    Task LogInsertAsync<TEntity>(
        DbConnection connection,
        string tableName,
        TEntity entity,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
        where TEntity : class;

    /// <summary>
    /// Audits an UPDATE from <paramref name="before"/> to <paramref name="after"/>: <c>ActionDesc</c> is the
    /// comma-separated list of the property names whose values differ (<see cref="AuditIgnoreAttribute"/> members
    /// excluded). When nothing changed no row is written.
    /// </summary>
    Task LogUpdateAsync<TEntity>(
        DbConnection connection,
        string tableName,
        TEntity before,
        TEntity after,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
        where TEntity : class;

    /// <summary>
    /// Audits a DELETE of <paramref name="entity"/>: same <c>PrimaryKeyValues</c> / <c>ActionDesc</c> rule as
    /// <see cref="LogInsertAsync{TEntity}"/>.
    /// </summary>
    Task LogDeleteAsync<TEntity>(
        DbConnection connection,
        string tableName,
        TEntity entity,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
        where TEntity : class;
}

/// <summary>
/// <c>UserName</c> is the signed-in user's <c>userName</c> JWT claim (falling back to <c>Identity.Name</c>, the
/// <c>userId</c> claim), or <see cref="SystemUserName"/> when the request is not authenticated. <c>DateTime</c> is
/// the injected <see cref="TimeProvider"/>'s UTC now, matching the existing rows (the frontend appends <c>Z</c>).
/// </summary>
public sealed class RowAuditWriter(IHttpContextAccessor httpContextAccessor, TimeProvider timeProvider) : IRowAuditWriter
{
    public const string Insert = "INSERT";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";

    /// <summary>Recorded as <c>UserName</c> when there is no authenticated user (background / anonymous work).</summary>
    public const string SystemUserName = "system";

    /// <summary>Name of the primary-key property looked up on every entity (case-insensitive).</summary>
    public const string PrimaryKeyPropertyName = "pkid";

    public const int TableNameMaxLength = 50;
    public const int UserNameMaxLength = 100;
    public const int PrimaryKeyValuesMaxLength = 100;
    public const int ActionTypeMaxLength = 20;
    public const int ActionDescMaxLength = 1000;

    /// <summary>Separator between property names in an UPDATE <c>ActionDesc</c>.</summary>
    public const string ChangedPropertySeparator = ", ";

    private const string Sql = """
        INSERT INTO RowAudit (TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime])
        VALUES (@TableName, @UserName, @PrimaryKeyValues, @ActionType, @ActionDesc, @DateTime);
        """;

    public Task LogInsertAsync<TEntity>(
        DbConnection connection,
        string tableName,
        TEntity entity,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        return WriteAsync(connection, tableName, PrimaryKeyValues(entity), Insert, FirstStringValue(entity), cancellationToken, transaction);
    }

    public async Task LogUpdateAsync<TEntity>(
        DbConnection connection,
        string tableName,
        TEntity before,
        TEntity after,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var changed = AuditHelper.ChangedColumns(before, after);
        if (changed.Count == 0)
        {
            // A save that changed nothing is not a change: a row here would move the "last changed" badge for no reason.
            return;
        }

        await WriteAsync(connection, tableName, PrimaryKeyValues(after), Update,
            string.Join(ChangedPropertySeparator, changed), cancellationToken, transaction);
    }

    public Task LogDeleteAsync<TEntity>(
        DbConnection connection,
        string tableName,
        TEntity entity,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        return WriteAsync(connection, tableName, PrimaryKeyValues(entity), Delete, FirstStringValue(entity), cancellationToken, transaction);
    }

    public Task WriteAsync(
        DbConnection connection,
        string tableName,
        string primaryKeyValues,
        string actionType,
        string? actionDesc,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(primaryKeyValues);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionType);

        var parameters = new
        {
            TableName = Truncate(tableName, TableNameMaxLength),
            UserName = Truncate(ResolveUserName(), UserNameMaxLength),
            PrimaryKeyValues = Truncate(primaryKeyValues, PrimaryKeyValuesMaxLength),
            ActionType = Truncate(actionType, ActionTypeMaxLength),
            ActionDesc = actionDesc is null ? null : Truncate(actionDesc, ActionDescMaxLength),
            DateTime = timeProvider.GetUtcNow().UtcDateTime
        };

        return connection.ExecuteAsync(new CommandDefinition(Sql, parameters, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// The entity's key as an invariant string: the property marked <see cref="AuditKeyAttribute"/> when there is
    /// one (string-keyed tables such as AppRole / AppUser), otherwise <c>pkid</c> (case-insensitive lookup). Throws
    /// when the entity has neither: such tables must use <see cref="WriteAsync"/> with an explicit key.
    /// </summary>
    public static string PrimaryKeyValues(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var type = entity.GetType();
        var property = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.CanRead && p.IsDefined(typeof(AuditKeyAttribute), inherit: true))
            ?? type.GetProperty(PrimaryKeyPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new InvalidOperationException(
                $"{type.Name} has no '{PrimaryKeyPropertyName}' or [AuditKey] property; call WriteAsync with the key value instead.");

        return Convert.ToString(property.GetValue(entity), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// Value of the first readable <see cref="string"/> property in declaration order, skipping the
    /// <see cref="AuditKeyAttribute"/> key and anything marked <see cref="AuditIgnoreAttribute"/>; <c>null</c> when
    /// the entity has no such property (or the value is null).
    /// </summary>
    public static string? FirstStringValue(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var property = entity.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.CanRead && p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0
                                 && !p.IsDefined(typeof(AuditKeyAttribute), inherit: true)
                                 && !p.IsDefined(typeof(AuditIgnoreAttribute), inherit: true));

        return (string?)property?.GetValue(entity);
    }

    private string ResolveUserName()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return SystemUserName;
        }

        var userName = user.FindFirst(JwtTokenIssuer.UserNameClaim)?.Value;
        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = user.Identity.Name;
        }

        return string.IsNullOrWhiteSpace(userName) ? SystemUserName : userName;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
