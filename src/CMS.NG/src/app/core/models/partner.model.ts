/** Row of dbo.Partner. `pkid` is a smallint IDENTITY assigned by the server. */
export interface Partner {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}

/** Create/update payload. `pkid` is ignored on create (send 0) and identifies the row on update. */
export type PartnerRequest = Partner;

/** Body of POST /api/partners/query. `null`/`undefined` means "no filter". */
export interface PartnerQuery {
  keyword?: string | null;
}

export const EMPTY_PARTNER_QUERY: PartnerQuery = {
  keyword: null
};

/** Kept as re-exports for existing imports; the implementation now lives in core/utils/ascii.validator.ts. */
export { ASCII_PATTERN, asciiValidator } from '@core/utils/ascii.validator';
