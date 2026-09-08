import { Component, inject } from '@angular/core';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { AuthService } from '@core/services/auth.service';
import { ChangePasswordFormComponent } from '@features/auth/change-password-form/change-password-form.component';

export const DEFAULT_PASSWORD_NOTICE =
  '您目前使用預設密碼登入，請先變更密碼後再繼續使用系統。（You signed in with the default password. Please change it before continuing.）';

/**
 * 變更密碼 Change Password page (`/change-password`). A login made with the system default password lands
 * here and cannot leave (guard + API 403) until the password is changed; the notice explains why. Any other
 * signed-in user may open it too, without the notice. The form itself is shared with 個人資料 My Profile.
 */
@Component({
  selector: 'app-change-password',
  imports: [CardModule, MessageModule, ChangePasswordFormComponent],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss'
})
export class ChangePasswordComponent {
  protected readonly auth = inject(AuthService);
  protected readonly notice = DEFAULT_PASSWORD_NOTICE;
}
