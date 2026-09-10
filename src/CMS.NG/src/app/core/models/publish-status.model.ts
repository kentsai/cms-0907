/** Row of dbo.PublishStatus. `pkid` is a user-assigned tinyint (0–255), not an identity. */
export interface PublishStatus {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

/** Create/update payload. Identical shape because the key is caller-assigned. */
export type PublishStatusRequest = PublishStatus;

/** Body of POST /api/publish-statuses/query. `null`/`undefined` means "no filter". */
export interface PublishStatusQuery {
  keyword?: string | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}

export const EMPTY_PUBLISH_STATUS_QUERY: PublishStatusQuery = {
  keyword: null,
  isDraft: null,
  isPublished: null,
  isDiscontinued: null
};
