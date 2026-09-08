/**
 * Date helpers for `date` columns, which travel over the API as `'yyyy-MM-dd'` strings.
 * Everything here works on LOCAL date components — never `toISOString()`, which converts to UTC
 * first and shifts the day for UTC+8 users.
 */

/** `Date` → `'yyyy-MM-dd'` (local). Returns null for null / invalid input. */
export function toIso(value: Date | null | undefined): string | null {
  if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
    return null;
  }
  const y = value.getFullYear();
  const m = String(value.getMonth() + 1).padStart(2, '0');
  const d = String(value.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** `'yyyy-MM-dd'` (or any string starting with it) → local-midnight `Date`. Returns null when unparseable. */
export function fromIso(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) {
    return null;
  }
  return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
}

/** Returns a new `Date` shifted by `years` (keeps month/day; Feb 29 rolls over per JS semantics). */
export function addYears(value: Date, years: number): Date {
  const result = new Date(value.getTime());
  result.setFullYear(result.getFullYear() + years);
  return result;
}

/** Returns a new local-midnight `Date` shifted by `days`. */
export function addDays(value: Date, days: number): Date {
  return new Date(value.getFullYear(), value.getMonth(), value.getDate() + days);
}

/** Monday (local midnight) of the week that contains `value`; Sunday belongs to the preceding Monday. */
export function startOfWeek(value: Date): Date {
  const offsetFromMonday = (value.getDay() + 6) % 7;
  return addDays(value, -offsetFromMonday);
}

/** Traditional Chinese weekday characters indexed by `Date.getDay()` (Sunday = 0). */
export const WEEKDAY_ZH: readonly string[] = ['日', '一', '二', '三', '四', '五', '六'];

/** `'M/d'` without zero padding, e.g. `3/16`. */
export function formatMonthDay(value: Date): string {
  return `${value.getMonth() + 1}/${value.getDate()}`;
}

/** `'M/d (一)'` — the day header used by the weekly boards. */
export function formatMonthDayWeekday(value: Date): string {
  return `${formatMonthDay(value)} (${WEEKDAY_ZH[value.getDay()]})`;
}
