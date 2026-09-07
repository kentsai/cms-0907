import { AbstractControl, ValidationErrors } from '@angular/forms';

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

/** Printable ASCII without whitespace — AppKey and ImageFilename are `varchar` columns. */
export const ASCII_PATTERN = /^[\x21-\x7E]+$/;

/**
 * Reactive-forms validator: the trimmed value must match ASCII_PATTERN. Blank / whitespace-only
 * input is left to `required` (or accepted for optional fields, where the form trims it to null).
 * Reports `{ pattern: true }` so templates can treat it like `Validators.pattern`.
 */
export function asciiValidator(control: AbstractControl<string | null>): ValidationErrors | null {
  const value = (control.value ?? '').trim();
  return value === '' || ASCII_PATTERN.test(value) ? null : { pattern: true };
}
