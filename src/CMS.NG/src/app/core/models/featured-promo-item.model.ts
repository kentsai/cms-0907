/**
 * Row of dbo.FeaturedPromoItem as returned by the API: one promotion placed in a slot (1–3) of a
 * training centre's home page on a given day. `scheduleOn` is `'yyyy-MM-dd'`; `promoCode` and
 * `trainingCenterName` are JOINed labels.
 */
export interface FeaturedPromoItem {
  pkid: number;
  scheduleOn: string;
  trainingCenterPkid: number;
  slot: number;
  promotionPkid: number;
  topic: string;
  description: string;
  trainingCenterName: string;
  promoCode: string;
}

/** Create/update payload. `pkid` is 0 on create. */
export type FeaturedPromoItemRequest = Omit<FeaturedPromoItem, 'trainingCenterName' | 'promoCode'>;

/** Response of GET /api/featured-promo-items/week. */
export interface FeaturedPromoWeek {
  weekStart: string;
  weekEnd: string;
  trainingCenterPkid: number;
  items: FeaturedPromoItem[];
}

/** What 複製 captures and 貼上 pre-fills: the promotion plus the editable text. */
export interface FeaturedPromoClipboard {
  promotionPkid: number;
  promoCode: string;
  topic: string;
  description: string;
}

export const FEATURED_PROMO_MIN_SLOT = 1;
export const FEATURED_PROMO_MAX_SLOT = 3;
export const FEATURED_PROMO_SLOTS: readonly number[] = [1, 2, 3];
