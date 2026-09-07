import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Printable ASCII without whitespace — for `varchar` columns (AppKey, ImageFilename, CourseId, ProdCourseId…). */
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
