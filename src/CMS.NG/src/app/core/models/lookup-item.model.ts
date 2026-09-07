/** Slim option returned by /api/lookups/* for FK dropdowns with a numeric key. */
export interface LookupItem {
  pkid: number;
  label: string;
}

/** Slim option for tables whose key is a string (AppRole.RoleId, AppUser.UserId). */
export interface StringLookupItem {
  id: string;
  label: string;
}
