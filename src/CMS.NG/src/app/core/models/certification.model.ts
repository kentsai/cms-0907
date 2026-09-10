/**
 * Row of dbo.Certification as returned by the API. `partnerName` is a JOINed label; the two
 * `*Pkids` lists are populated only by GET /{id} (list/query rows carry empty arrays).
 * `title` is nchar(100) in the DB and comes back RTRIMmed, or null.
 */
export interface Certification {
  pkid: number;
  partnerPkid: number;
  title: string | null;
  partnerName: string;
  coursePkids: number[];
  jobCategoryPkids: number[];
}

/** Create/update payload: the scalars plus both N-N id lists. `pkid` is 0 on create. */
export type CertificationRequest = Omit<Certification, 'partnerName'>;

/** Body of POST /api/certifications/query. `null`/`undefined` means "no filter". */
export interface CertificationQuery {
  keyword?: string | null;
  partnerPkid?: number | null;
  coursePkid?: number | null;
  jobCategoryPkid?: number | null;
}

export const EMPTY_CERTIFICATION_QUERY: CertificationQuery = {
  keyword: null,
  partnerPkid: null,
  coursePkid: null,
  jobCategoryPkid: null
};

/** Display label for a certification whose Title is null. */
export const UNTITLED_CERTIFICATION = '(無名稱)';

export function certificationLabel(item: Pick<Certification, 'title'>): string {
  return item.title ?? UNTITLED_CERTIFICATION;
}
