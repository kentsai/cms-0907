using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAppRoleRepository
{
    Task<IReadOnlyList<AppRole>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken);

    /// <summary>Returns the role with <see cref="AppRole.UserIds"/> populated, or null.</summary>
    Task<AppRole?> GetByIdAsync(string roleId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string roleId, CancellationToken cancellationToken);

    /// <summary>Inserts the role and its user assignments; returns the new identity pkid.</summary>
    Task<int> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken);

    /// <summary>Updates scalar columns and re-syncs user assignments. False when no row matched.</summary>
    Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes user assignments then the role. False when no row matched.</summary>
    Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken);

    /// <summary>Slim list for FK dropdowns: id = RoleId, label = RoleName, ordered by RoleId.</summary>
    Task<IReadOnlyList<StringLookupItem>> GetLookupAsync(CancellationToken cancellationToken);
}
