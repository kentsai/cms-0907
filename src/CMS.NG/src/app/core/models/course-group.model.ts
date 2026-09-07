/** Row of dbo.CourseGroup. `pkid` is a smallint IDENTITY assigned by the server. */
export interface CourseGroup {
  pkid: number;
  description: string;
}

/** Create/update payload. `pkid` is ignored on create (send 0) and identifies the row on update. */
export type CourseGroupRequest = CourseGroup;

/** Body of POST /api/course-groups/query. `null`/`undefined` means "no filter". */
export interface CourseGroupQuery {
  keyword?: string | null;
}

export const EMPTY_COURSE_GROUP_QUERY: CourseGroupQuery = {
  keyword: null
};
