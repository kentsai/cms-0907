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
