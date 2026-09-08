import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Minimum length of a user-chosen password (mirrors `PasswordPolicy.MinLength` in the API). */
export const PASSWORD_MIN_LENGTH = 8;
/** How many of the four character classes a password must contain (mirrors `PasswordPolicy.MinCharacterClasses`). */
export const PASSWORD_MIN_CLASSES = 3;

/** Shown under the new-password field and in the error toast; the API returns the Chinese half of it. */
export const PASSWORD_POLICY_MESSAGE =
  '密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號' +
  '（Password must be at least 8 characters and contain at least 3 of the 4 classes: uppercase / lowercase / digit / symbol）';

/** The four classes. A symbol is any other printable ASCII character; whitespace and CJK count towards none. */
const CLASS_PATTERNS = [/[A-Z]/, /[a-z]/, /[0-9]/, /[!-/:-@[-`{-~]/];

/** Same rule as `PasswordPolicy.IsCompliant` in the API. */
export function meetsPasswordPolicy(password: string | null | undefined): boolean {
  if (!password || password.length < PASSWORD_MIN_LENGTH) {
    return false;
  }
  const classes = CLASS_PATTERNS.filter(pattern => pattern.test(password)).length;
  return classes >= PASSWORD_MIN_CLASSES;
}

/**
 * Reactive-forms validator for a new password. Blank input is left to `required`; anything else must pass
 * `meetsPasswordPolicy`. Reports `{ passwordPolicy: true }`.
 */
export function passwordPolicyValidator(control: AbstractControl<string | null>): ValidationErrors | null {
  const value = control.value ?? '';
  return value === '' || meetsPasswordPolicy(value) ? null : { passwordPolicy: true };
}

/**
 * Group-level validator: the two named controls must hold exactly the same value (no trimming, case-sensitive).
 * Reports `{ passwordMismatch: true }` on the group once the confirmation has been typed.
 */
export function passwordsMatchValidator(
  passwordField: string,
  confirmField: string
): (group: AbstractControl) => ValidationErrors | null {
  return group => {
    const password = group.get(passwordField)?.value ?? '';
    const confirm = group.get(confirmField)?.value ?? '';
    return confirm === '' || password === confirm ? null : { passwordMismatch: true };
  };
}
