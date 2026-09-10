import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AuthService, LOGIN_PATH, PASSWORD_CHANGED_REASON } from '@core/services/auth.service';
import { PASSWORD_POLICY_MESSAGE, passwordPolicyValidator, passwordsMatchValidator } from '@core/utils/password.validator';

type PasswordField = 'currentPassword' | 'newPassword' | 'confirmNewPassword';

/** Field error keys as the API's `ValidationProblem` spells them (PascalCase) → form control names. */
const SERVER_FIELD_MAP: Record<string, PasswordField> = {
  currentpassword: 'currentPassword',
  newpassword: 'newPassword',
  confirmnewpassword: 'confirmNewPassword'
};

export const CURRENT_PASSWORD_REQUIRED_MESSAGE = '請輸入目前密碼（Current password is required）';
export const NEW_PASSWORD_REQUIRED_MESSAGE = '請輸入新密碼（New password is required）';
export const CONFIRM_PASSWORD_REQUIRED_MESSAGE = '請再次輸入新密碼（Please confirm the new password）';
export const PASSWORD_MISMATCH_MESSAGE = '新密碼與確認新密碼不一致（New password and confirmation do not match）';
export const PASSWORD_CHANGED_MESSAGE = '密碼已變更，請使用新密碼重新登入。';

/**
 * 變更密碼 Change Password form: three plain-text password fields POSTed to `/auth/change-password`; the API
 * identifies the user by the token and no password hash ever reaches the browser. Client validation mirrors
 * the API (required, policy on the new password, confirmation must match) so invalid input never calls it.
 * A successful change ends the session: the API rejects the current token from then on, so the form clears
 * the stored profile and returns to `/login?reason=password-changed`. Used by the 個人資料 page and by the
 * standalone change-password page a default-password login is held on.
 */
@Component({
  selector: 'app-change-password-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule],
  templateUrl: './change-password-form.component.html',
  styleUrl: './change-password-form.component.scss'
})
export class ChangePasswordFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  private readonly auth = inject(AuthService);

  protected readonly changingPassword = signal(false);
  protected readonly policyMessage = PASSWORD_POLICY_MESSAGE;

  /** Client-side mirror of the API rules: required, policy on the new password, confirmation must match. */
  protected readonly passwordForm = this.fb.nonNullable.group(
    {
      currentPassword: this.fb.nonNullable.control('', [Validators.required]),
      newPassword: this.fb.nonNullable.control('', [Validators.required, passwordPolicyValidator]),
      confirmNewPassword: this.fb.nonNullable.control('', [Validators.required])
    },
    { validators: passwordsMatchValidator('newPassword', 'confirmNewPassword') }
  );

  /**
   * The message to show under a password field, or null when there is nothing to show yet. A message the
   * API returned for the field (`{ server }`) wins; otherwise required → policy → mismatch, once touched.
   */
  protected passwordError(field: PasswordField): string | null {
    const control = this.passwordForm.controls[field];
    if (!(control.touched || control.dirty)) {
      return null;
    }
    if (typeof control.errors?.['server'] === 'string') {
      return control.errors['server'] as string;
    }
    if (control.errors?.['required']) {
      return field === 'currentPassword'
        ? CURRENT_PASSWORD_REQUIRED_MESSAGE
        : field === 'newPassword'
          ? NEW_PASSWORD_REQUIRED_MESSAGE
          : CONFIRM_PASSWORD_REQUIRED_MESSAGE;
    }
    if (control.errors?.['passwordPolicy']) {
      return PASSWORD_POLICY_MESSAGE;
    }
    if (field === 'confirmNewPassword' && this.passwordForm.errors?.['passwordMismatch']) {
      return PASSWORD_MISMATCH_MESSAGE;
    }
    return null;
  }

  protected changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.changingPassword.set(true);

    this.auth.changePassword(this.passwordForm.getRawValue()).subscribe({
      next: () => {
        this.changingPassword.set(false);
        this.passwordForm.reset();
        // The token this page holds is now rejected by the API; end the session and ask for a fresh login.
        this.auth.clear();
        this.messages.add({ severity: 'success', summary: '密碼已變更', detail: PASSWORD_CHANGED_MESSAGE });
        void this.router.navigate([LOGIN_PATH], { queryParams: { reason: PASSWORD_CHANGED_REASON } });
      },
      error: (err: { status?: number; error?: { errors?: Record<string, string[]> } }) => {
        this.changingPassword.set(false);
        const detail =
          err?.status === 400
            ? this.applyServerErrors(err.error?.errors)
            : err?.status === 404
              ? '找不到使用者資料。'
              : '變更密碼失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '變更密碼失敗', detail });
      }
    });
  }

  /** Pins each API field error under its input and returns the first message for the toast. */
  private applyServerErrors(errors: Record<string, string[]> | undefined): string {
    let first: string | null = null;
    for (const [key, messages] of Object.entries(errors ?? {})) {
      const field = SERVER_FIELD_MAP[key.toLowerCase()];
      const message = messages?.[0];
      if (!field || !message) {
        continue;
      }
      // The API sends the Chinese rule only; show the bilingual copy the user already sees on the client.
      const shown = field === 'newPassword' && message.startsWith('密碼長度') ? PASSWORD_POLICY_MESSAGE : message;
      this.passwordForm.controls[field].setErrors({ server: shown });
      this.passwordForm.controls[field].markAsTouched();
      first ??= shown;
    }
    return first ?? '請檢查輸入的密碼。';
  }
}
