import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { AuthService, CHANGE_PASSWORD_PATH, PASSWORD_CHANGED_REASON } from '@core/services/auth.service';

export const PASSWORD_CHANGED_NOTICE = '密碼已變更，請使用新密碼重新登入。（Your password was changed. Please sign in again with the new password.）';

/**
 * Public sign-in page. On success the profile is stored in session storage and the user is sent to `returnUrl`
 * (or `/`). `?reason=password-changed` (set by the profile page after a password change) shows an info notice.
 */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, ButtonModule, CardModule, InputTextModule, MessageModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  /** Why the user landed here, when the app sent them (only the password-change reason is known). */
  protected readonly notice =
    this.route.snapshot.queryParamMap.get('reason') === PASSWORD_CHANGED_REASON ? PASSWORD_CHANGED_NOTICE : null;

  protected readonly form = this.fb.nonNullable.group({
    userId: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    password: this.fb.nonNullable.control('', [Validators.required])
  });

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { userId, password } = this.form.getRawValue();
    this.loading.set(true);
    this.errorMessage.set(null);

    this.auth.login({ userId: userId.trim(), password }).subscribe({
      next: () => {
        this.loading.set(false);
        // A default-password login may only change its password: go straight there, whatever returnUrl says.
        this.router.navigateByUrl(this.auth.mustChangePassword() ? CHANGE_PASSWORD_PATH : this.safeReturnUrl());
      },
      error: (err: { status?: number; error?: { message?: string } }) => {
        this.loading.set(false);
        this.form.controls.password.reset();
        this.errorMessage.set(
          err?.status === 401 ? (err.error?.message ?? '帳號或密碼錯誤。') : '登入失敗，請稍後再試。'
        );
      }
    });
  }

  /** Only an in-app absolute path is honoured, so `returnUrl` cannot send the user to another site. */
  private safeReturnUrl(): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '';
    return returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/';
  }
}
