import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { AuthService } from '@core/services/auth.service';
import { ChangePasswordFormComponent } from '@features/auth/change-password-form/change-password-form.component';

/**
 * 個人資料 My Profile: the signed-in user's UserId and roles (read-only, from the stored profile / token),
 * an editable UserName, and a separate 變更密碼 Change Password card (`ChangePasswordFormComponent`, shared
 * with the standalone change-password page). Saving the name PUTs only the UserName; the API identifies the
 * user by the token.
 */
@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, ButtonModule, CardModule, InputTextModule, TagModule, ToolbarModule, ChangePasswordFormComponent],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss'
})
export class ProfileComponent {
  private readonly fb = inject(FormBuilder);
  private readonly messages = inject(MessageService);
  protected readonly auth = inject(AuthService);

  protected readonly saving = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    userName: this.fb.nonNullable.control(this.auth.userName(), [
      Validators.required,
      Validators.maxLength(200),
      noWhitespaceOnly
    ])
  });

  protected get userNameInvalid(): boolean {
    const control = this.form.controls.userName;
    return control.invalid && (control.touched || control.dirty);
  }

  protected reset(): void {
    this.form.reset({ userName: this.auth.userName() });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const userName = this.form.controls.userName.value.trim();
    this.saving.set(true);

    this.auth.updateProfile(userName).subscribe({
      next: updated => {
        this.saving.set(false);
        this.form.reset({ userName: updated.userName });
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `姓名已更新為「${updated.userName}」。` });
      },
      error: (err: { status?: number; error?: { errors?: Record<string, string[]> } }) => {
        this.saving.set(false);
        const detail =
          err?.status === 400
            ? (err.error?.errors?.['UserName']?.[0] ?? '請輸入姓名。')
            : err?.status === 404
              ? '找不到使用者資料。'
              : '儲存失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '儲存失敗', detail });
      }
    });
  }
}

/** `Validators.required` accepts "   "; the API trims and rejects it, so the form does too. */
function noWhitespaceOnly(control: AbstractControl<string>): { whitespace: true } | null {
  return control.value.trim().length === 0 ? { whitespace: true } : null;
}
