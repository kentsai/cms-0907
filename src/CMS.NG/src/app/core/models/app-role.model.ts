/** Row of dbo.AppRole. The primary key is the string `roleId`; `pkid` is a display-only identity. */
export interface AppRole {
  pkid: number;
  roleId: string;
  roleName: string;
  permissionLevel: number;
  description: string | null;
  /** Count of AppUserRole rows (computed server-side). */
  userCount: number;
  /** Assigned user ids; populated by getById only. */
  userIds: string[];
}

/** Create/update payload. `roleId` is caller-assigned and immutable on update. */
export interface AppRoleRequest {
  roleId: string;
  roleName: string;
  permissionLevel: number;
  description: string | null;
  userIds: string[];
}

/** Body of POST /api/app-roles/query. `null`/`undefined` means "no filter". */
export interface AppRoleQuery {
  keyword?: string | null;
  permissionLevel?: number | null;
  userId?: string | null;
}

export const EMPTY_APP_ROLE_QUERY: AppRoleQuery = {
  keyword: null,
  permissionLevel: null,
  userId: null
};

export const DEFAULT_PERMISSION_LEVEL = 100;
