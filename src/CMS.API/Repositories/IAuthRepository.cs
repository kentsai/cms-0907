using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for the login / self-service profile flow. Verification itself happens in <c>AuthController</c>.</summary>
public interface IAuthRepository
{
    /// <summary>The <c>AppUser</c> row (including <c>PasswordHash</c>) for <paramref name="userId"/>, or null when there is none.</summary>
    Task<AppUserCredential?> GetCredentialAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Every <c>AppUserRole.RoleId</c> for the user, ordered by RoleId.</summary>
    Task<IReadOnlyList<string>> GetRoleIdsAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// <c>SysConfig.appConfig.symmetricSecurityKey</c>, read at call time so a rotated key takes effect without a restart.
    /// Throws <see cref="Infrastructure.AppConfigException"/> when it cannot be resolved.
    /// </summary>
    Task<string> GetSymmetricSecurityKeyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// <c>SysConfig.appConfig.defaultPassword</c> (the password admins seed new / reset accounts with), read at call
    /// time. Login compares the presented password with it to flag a session that must change its password.
    /// Throws <see cref="Infrastructure.AppConfigException"/> when it cannot be resolved.
    /// </summary>
    Task<string> GetDefaultPasswordAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sets <c>AppUser.UserName</c> for <paramref name="userId"/> (already trimmed and non-empty) and writes the RowAudit.
    /// Returns false when no row matched. Nothing else on the row is touched.
    /// </summary>
    Task<bool> UpdateUserNameAsync(string userId, string userName, CancellationToken cancellationToken);

    /// <summary>
    /// Sets <c>AppUser.PasswordHash</c> (already hashed, see <see cref="Infrastructure.PasswordHasher.Hash"/>) and
    /// <c>PasswordUpdatedTime</c> for <paramref name="userId"/> and writes the RowAudit. Returns false when no row
    /// matched. The caller (<c>AuthController</c>) has already verified the current password and the policy.
    /// </summary>
    Task<bool> UpdatePasswordAsync(string userId, string passwordHash, DateTime passwordUpdatedTime, CancellationToken cancellationToken);

    /// <summary>
    /// Rewrites <c>AppUser.PasswordHash</c> in the current storage format for a user who just signed in with a
    /// legacy hash (see <see cref="Infrastructure.PasswordHasher.Verify"/>). Returns false when no row matched.
    /// <para>
    /// Deliberately touches nothing else. <c>PasswordUpdatedTime</c> stays as it was, because the password itself
    /// did not change: moving it would revoke every live session of that user for a storage detail. No RowAudit row
    /// is written either — the 異動紀錄 History badge shows changes a person made, and a re-encoding is not one.
    /// </para>
    /// </summary>
    Task<bool> UpgradePasswordHashAsync(string userId, string passwordHash, CancellationToken cancellationToken);

    /// <summary>
    /// The user's <c>PasswordUpdatedTime</c> (UTC), or null when there is no such user. Read by the bearer handler
    /// through <see cref="Infrastructure.IPasswordStampCache"/> — never selects <c>PasswordHash</c>.
    /// </summary>
    Task<PasswordStamp?> GetPasswordStampAsync(string userId, CancellationToken cancellationToken);
}
