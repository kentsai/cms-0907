/**
 * Row of dbo.AppUser. The primary key is the string `userId`; `pkid` is a display-only identity.
 * The password hash is never sent by the API and has no counterpart here.
 */
export interface AppUser {
  pkid: number;
  userId: string;
  userName: string;
  isActive: boolean;
  /** Server-managed (set on create and on password reset). `datetime` without zone — append 'Z' before formatting. */
  passwordUpdatedTime: string | null;
  /** Count of AppUserRole rows (computed server-side). */
  roleCount: number;
  /** Assigned role ids; populated by getById only. */
  roleIds: string[];
}

/** Create/update payload. `userId` is caller-assigned and immutable on update. No password field by design. */
export interface AppUserRequest {
  userId: string;
  userName: string;
  isActive: boolean;
  roleIds: string[];
}

/** Body of POST /api/app-users/query. `null`/`undefined` means "no filter". */
export interface AppUserQuery {
  keyword?: string | null;
  isActive?: boolean | null;
  roleId?: string | null;
}

export const EMPTY_APP_USER_QUERY: AppUserQuery = {
  keyword: null,
  isActive: null,
  roleId: null
};
