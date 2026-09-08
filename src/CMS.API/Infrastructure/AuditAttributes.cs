namespace CMS.API.Infrastructure;

/// <summary>
/// Marks the property whose value is recorded as <c>RowAudit.PrimaryKeyValues</c> instead of the default
/// <c>pkid</c>. Used on tables whose real (clustered) key is a string and whose <c>pkid</c> is only an IDENTITY
/// surrogate (<c>AppRole.RoleId</c>, <c>AppUser.UserId</c>), so the audit badge can look the rows up by that key.
/// The key is never used as the INSERT / DELETE description (it is already in <c>PrimaryKeyValues</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class AuditKeyAttribute : Attribute;

/// <summary>
/// Excludes a response-model property from row auditing: it is skipped by the UPDATE changed-column diff and
/// never used as an INSERT / DELETE description. Put it on members that are not columns of the audited table —
/// JOINed label columns (<c>PartnerName</c>), subquery counts (<c>UserCount</c>) and the like.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class AuditIgnoreAttribute : Attribute;
