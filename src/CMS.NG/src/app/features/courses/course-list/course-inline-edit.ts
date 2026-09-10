import { Course } from '@core/models/course.model';
import { ASCII_PATTERN } from '@core/utils/ascii.validator';
import { fromIso, toIso } from '@core/utils/date.util';

/**
 * Columns of the Course list that can be edited in place. `pkid` (IDENTITY), `partnerName` and
 * `courseGroupDescription` (JOINed FK labels) are deliberately absent and stay read-only.
 */
export type EditableCourseField =
  | 'displayOrder'
  | 'courseId'
  | 'prodCourseId'
  | 'title'
  | 'publishStatusPkid'
  | 'scheduleOn'
  | 'scheduleOff'
  | 'hour'
  | 'listPrice'
  | 'learningCredit'
  | 'canRepeat';

export const EDITABLE_COURSE_FIELDS: readonly EditableCourseField[] = [
  'displayOrder', 'courseId', 'prodCourseId', 'title', 'publishStatusPkid',
  'scheduleOn', 'scheduleOff', 'hour', 'listPrice', 'learningCredit', 'canRepeat'
];

export function isEditableCourseField(field: string): field is EditableCourseField {
  return (EDITABLE_COURSE_FIELDS as readonly string[]).includes(field);
}

/** Draft value bound to the cell editor. Dates are `Date` objects (p-datepicker); everything else is the model type. */
export type CellDraft = string | number | boolean | Date | null;

/** The single cell currently in edit mode, keyed by row pkid + column. */
export interface CellEdit {
  pkid: number;
  field: EditableCourseField;
  value: CellDraft;
  /** Inline validation message; while set the cell stays in edit mode. */
  error: string | null;
}

/** Model value → editor draft (ISO date strings become local-midnight Dates). */
export function toCellDraft(row: Course, field: EditableCourseField): CellDraft {
  if (field === 'scheduleOn' || field === 'scheduleOff') {
    return fromIso(row[field]);
  }
  return row[field];
}

/**
 * Validates a draft against the same rules as the Course form. Returns the zh-TW message to show
 * inline, or null when the value may be persisted. Date-range rule compares against the row's
 * other schedule date.
 */
export function validateCourseCell(row: Course, field: EditableCourseField, value: CellDraft): string | null {
  switch (field) {
    case 'title':
      return requiredText(value, 200, '請輸入課程名稱（最多 200 字）');
    case 'courseId':
      return requiredText(value, 50, '請輸入簡介代碼（最多 50 字）') ?? asciiOnly(value);
    case 'prodCourseId':
      return requiredText(value, 50, '請輸入科目代碼（最多 50 字）') ?? asciiOnly(value);
    case 'displayOrder':
      return isFiniteNumber(value) ? null : '請輸入顯示順序';
    case 'hour':
      return nonNegative(value, '請輸入時數（0 以上）');
    case 'listPrice':
      return nonNegative(value, '請輸入定價（0 以上）');
    case 'learningCredit':
      return nonNegative(value, '請輸入點數（0 以上）');
    case 'publishStatusPkid':
      return isFiniteNumber(value) && value > 0 ? null : '請選擇上架狀態';
    case 'scheduleOn': {
      if (!isValidDate(value)) {
        return '請選擇上架日期';
      }
      const off = fromIso(row.scheduleOff);
      return off && value.getTime() > off.getTime() ? '上架日期不可晚於下架日期' : null;
    }
    case 'scheduleOff': {
      if (!isValidDate(value)) {
        return '請選擇下架日期';
      }
      const on = fromIso(row.scheduleOn);
      return on && value.getTime() < on.getTime() ? '下架日期不可早於上架日期' : null;
    }
    case 'canRepeat':
      return typeof value === 'boolean' ? null : '請選擇是否允許重聽';
  }
}

/** Editor draft → value stored on the row / sent to the API (trimmed text, ISO dates, plain numbers). */
export function toModelValue(field: EditableCourseField, value: CellDraft): Course[EditableCourseField] {
  switch (field) {
    case 'title':
    case 'courseId':
    case 'prodCourseId':
      return String(value ?? '').trim();
    case 'scheduleOn':
    case 'scheduleOff':
      return toIso(value as Date)!;
    case 'canRepeat':
      return value === true;
    default:
      return Number(value);
  }
}

function requiredText(value: CellDraft, maxLength: number, message: string): string | null {
  const text = typeof value === 'string' ? value.trim() : '';
  return text.length === 0 || text.length > maxLength ? message : null;
}

function asciiOnly(value: CellDraft): string | null {
  return ASCII_PATTERN.test(String(value ?? '').trim()) ? null : '只能輸入英數字與符號（不可含空白或中文）';
}

function isFiniteNumber(value: CellDraft): value is number {
  return typeof value === 'number' && Number.isFinite(value);
}

function nonNegative(value: CellDraft, message: string): string | null {
  return isFiniteNumber(value) && value >= 0 ? null : message;
}

function isValidDate(value: CellDraft): value is Date {
  return value instanceof Date && !Number.isNaN(value.getTime());
}
