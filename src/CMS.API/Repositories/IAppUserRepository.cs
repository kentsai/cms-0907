using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAppUserRepository
{
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AppUser>> QueryAsync(AppUserQuery query, CancellationToken cancellationToken);

    /// <summary>Returns the user with <see cref="AppUser.RoleIds"/> populated, or null.</summary>
    Task<AppUser?> GetByIdAsync(string userId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the user with <c>PasswordHash</c> = SHA-256 of <c>SysConfig.appConfig.defaultPassword</c>, syncs the
    /// role assignments, and returns the new identity pkid. Throws <see cref="Infrastructure.AppConfigException"/>
    /// when the default password cannot be resolved.
    /// </summary>
    Task<int> CreateAsync(AppUserRequest request, CancellationToken cancellationToken);

    /// <summary>Updates <c>UserName</c> / <c>IsActive</c> and re-syncs roles. Never touches the password. False when no row matched.</summary>
    Task<bool> UpdateAsync(AppUserRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes role assignments then the user. False when no row matched.</summary>
    Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Re-hashes the current default password into <c>PasswordHash</c> and stamps <c>PasswordUpdatedTime</c>.
    /// False when no row matched. Throws <see cref="Infrastructure.AppConfigException"/> when the default password cannot be resolved.
    /// </summary>
    Task<bool> ResetPasswordAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Slim list for FK dropdowns: id = UserId, label = "UserName (UserId)", ordered by UserName.</summary>
    Task<IReadOnlyList<StringLookupItem>> GetLookupAsync(CancellationToken cancellationToken);
}
