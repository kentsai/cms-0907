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

/** Row of the PromoCode search (/api/lookups/promotions?keyword=); carries the promotion's own text for pre-fill. */
export interface PromotionLookupItem {
  pkid: number;
  promoCode: string;
  topic: string;
  description: string;
}
