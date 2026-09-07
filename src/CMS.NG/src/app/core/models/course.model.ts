/**
 * Row of dbo.Course as returned by the API. `date` columns arrive as `'yyyy-MM-dd'` strings.
 * The three `*Name` / `*Description` members are JOINed labels; the two `*Pkids` lists are
 * populated only by GET /{id}.
 */
export interface Course {
  pkid: number;
  title: string;
  officialTitle: string | null;
  courseId: string;
  prodCourseId: string;
  friendlyUrl: string;
  displayOrder: number;
  partnerPkid: number;
  courseGroupPkid: number | null;
  publishStatusPkid: number;
  scheduleOn: string;
  scheduleOff: string;
  hour: number;
  listPrice: number;
  learningCredit: number;
  material: string | null;
  objective: string | null;
  target: string | null;
  prerequisites: string | null;
  outline: string | null;
  towardCertOrExam: string | null;
  note: string | null;
  otherInfo: string | null;
  canRepeat: boolean;
  partnerName: string;
  courseGroupDescription: string | null;
  publishStatusDescription: string;
  certificationPkids: number[];
  jobCategoryPkids: number[];
}

/** Create/update payload: the scalars plus both N-N id lists. `pkid` is 0 on create. */
export type CourseRequest = Omit<Course, 'partnerName' | 'courseGroupDescription' | 'publishStatusDescription'>;

/** Body of POST /api/courses/query. `null`/`undefined` means "no filter"; dates are `'yyyy-MM-dd'`. */
export interface CourseQuery {
  keyword?: string | null;
  partnerPkid?: number | null;
  courseGroupPkid?: number | null;
  publishStatusPkid?: number | null;
  scheduleOnFrom?: string | null;
  scheduleOnTo?: string | null;
  scheduleOffFrom?: string | null;
  scheduleOffTo?: string | null;
  canRepeat?: boolean | null;
}

export const EMPTY_COURSE_QUERY: CourseQuery = {
  keyword: null,
  partnerPkid: null,
  courseGroupPkid: null,
  publishStatusPkid: null,
  scheduleOnFrom: null,
  scheduleOnTo: null,
  scheduleOffFrom: null,
  scheduleOffTo: null,
  canRepeat: null
};
