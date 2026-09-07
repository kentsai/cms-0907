using System.Data.Common;

namespace CMS.API.Infrastructure;

/// <summary>
/// Creates open ADO.NET connections for Dapper to execute against.
/// </summary>
public interface IDbConnectionFactory
{
    Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
