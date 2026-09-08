import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { AUTH_PROFILE_KEY, AuthService, LOGIN_PATH, PASSWORD_CHANGED_REASON } from '@core/services/auth.service';
import { UserProfile } from '@core/models/auth.model';
import { seedSignedInUser } from '@app/testing/auth-testing';
import { ProfileComponent } from './profile.component';

describe('ProfileComponent', () => {
  const profileUrl = `${environment.apiBaseUrl}/auth/profile`;
  let fixture: ComponentFixture<ProfileComponent>;
  let backend: HttpTestingController;
  let auth: AuthService;
  let navigate: jasmine.Spy;
  let messages: jasmine.SpyObj<MessageService>;

  async function mount(): Promise<void> {
    messages = jasmine.createSpyObj<MessageService>('MessageService', ['add']);
    await TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MessageService, useValue: messages },
        ConfirmationService
      ]
    }).compileComponents();

    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;
  const userNameInput = () => el().querySelector<HTMLInputElement>('#userName')!;

  function typeUserName(value: string): void {
    const input = userNameInput();
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function submit(): void {
    el().querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    backend.verify();
    sessionStorage.clear();
  });

  it('shows the UserId read-only', async () => {
    seedSignedInUser(['Admin'], '陳小美');
    await mount();

    const userId = el().querySelector<HTMLInputElement>('#userId')!;
    expect(userId.value).toBe('mei');
    expect(userId.readOnly).toBeTrue();
  });

  it('shows the roles from the token as read-only tags, with no input for them', async () => {
    seedSignedInUser(['Admin', 'Editor'], '陳小美');
    await mount();

    const roles = el().querySelector('[data-testid="roles"]')!;
    expect(roles.textContent).toContain('Admin');
    expect(roles.textContent).toContain('Editor');
    expect(roles.querySelectorAll('input, select, textarea').length).toBe(0);
    expect(el().querySelectorAll('#profileForm input').length).toBe(2); // userId (readonly) + userName only
  });

  it('pre-fills the editable UserName with the current name', async () => {
    seedSignedInUser(['Admin'], '陳小美');
    await mount();

    expect(userNameInput().value).toBe('陳小美');
    expect(userNameInput().readOnly).toBeFalse();
  });

  it('saving PUTs only { userName } and refreshes session storage and the shell name', async () => {
    const seeded = seedSignedInUser(['Admin'], '陳小美');
    await mount();

    typeUserName('  陳大美  ');
    submit();

    const req = backend.expectOne(profileUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ userName: '陳大美' });
    req.flush({ userId: 'mei', userName: '陳大美', roles: ['Admin'] });
    fixture.detectChanges();

    const stored = JSON.parse(sessionStorage.getItem(AUTH_PROFILE_KEY)!) as UserProfile;
    expect(stored.userName).toBe('陳大美');
    expect(stored.userId).toBe('mei');
    expect(stored.accessToken).toBe(seeded.accessToken);
    expect(auth.userName()).toBe('陳大美'); // what the app shell binds to
    expect(userNameInput().value).toBe('陳大美');
    expect(messages.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success' }));
  });

  it('does not call the API for an empty or whitespace UserName', async () => {
    seedSignedInUser(['Admin'], '陳小美');
    await mount();

    typeUserName('   ');
    submit();

    backend.expectNone(profileUrl);
    expect(el().textContent).toContain('請輸入姓名');
    expect(auth.userName()).toBe('陳小美');
  });

  it('shows an error toast and keeps the old name when the API rejects the change', async () => {
    seedSignedInUser(['Admin'], '陳小美');
    await mount();

    typeUserName('陳大美');
    submit();
    backend.expectOne(profileUrl).flush(
      { errors: { UserName: ['請輸入姓名。'] } },
      { status: 400, statusText: 'Bad Request' }
    );
    fixture.detectChanges();

    expect(auth.userName()).toBe('陳小美');
    expect(messages.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error', detail: '請輸入姓名。' }));
  });

  it('還原 restores the stored name', async () => {
    seedSignedInUser(['Admin'], '陳小美');
    await mount();

    typeUserName('打錯了');
    el().querySelectorAll<HTMLButtonElement>('button')[0].click(); // 還原 is the first toolbar button
    fixture.detectChanges();

    expect(userNameInput().value).toBe('陳小美');
  });

  describe('變更密碼 Change Password', () => {
    const changePasswordUrl = `${environment.apiBaseUrl}/auth/change-password`;
    const passwordInput = (id: string) => el().querySelector<HTMLInputElement>(`#passwordForm #${id}`)!;
    const errorText = (id: string) => el().querySelector(`[data-testid="${id}-error"]`)?.textContent ?? '';

    function typePassword(id: string, value: string): void {
      const input = passwordInput(id);
      input.value = value;
      input.dispatchEvent(new Event('input'));
      input.dispatchEvent(new Event('blur'));
      fixture.detectChanges();
    }

    function fill(current: string, next: string, confirm = next): void {
      typePassword('currentPassword', current);
      typePassword('newPassword', next);
      typePassword('confirmNewPassword', confirm);
    }

    function submitPasswordForm(): void {
      el().querySelector('#passwordForm')!.dispatchEvent(new Event('submit'));
      fixture.detectChanges();
    }

    beforeEach(async () => {
      seedSignedInUser(['Admin'], '陳小美');
      await mount();
    });

    it('renders three masked password inputs and no hash anywhere', () => {
      const inputs = el().querySelectorAll<HTMLInputElement>('#passwordForm input');
      expect(Array.from(inputs, i => i.id)).toEqual(['currentPassword', 'newPassword', 'confirmNewPassword']);
      expect(Array.from(inputs).every(i => i.type === 'password')).toBeTrue();
      expect(Array.from(inputs).every(i => i.value === '')).toBeTrue();
      expect(el().textContent).toContain('變更密碼 Change Password');
    });

    it('shows the bilingual policy as a hint before anything is typed', () => {
      expect(el().textContent).toContain('密碼長度至少需 8 碼');
      expect(el().textContent).toContain('uppercase / lowercase / digit / symbol');
      expect(errorText('newPassword')).toBe('');
    });

    it('does not call the API and marks every field required when submitted empty', () => {
      submitPasswordForm();

      backend.expectNone(changePasswordUrl);
      expect(errorText('currentPassword')).toContain('請輸入目前密碼');
      expect(errorText('newPassword')).toContain('請輸入新密碼');
      expect(errorText('confirmNewPassword')).toContain('請再次輸入新密碼');
    });

    for (const weak of ['Ab1!', 'Abcdef1', 'abcdefgh', 'abcdefg1', 'ABCDEFG1', 'Abcdefgh', '1234567!']) {
      it(`rejects the new password "${weak}" client-side with the bilingual rule`, () => {
        fill('Old12345!', weak);
        submitPasswordForm();

        backend.expectNone(changePasswordUrl);
        expect(errorText('newPassword')).toContain('密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號');
        expect(errorText('newPassword')).toContain('Password must be at least 8 characters');
      });
    }

    for (const strong of ['Abcdefg1', 'abcdefg1!', 'ABCDEFG1!', 'Abcdefg!']) {
      it(`accepts the new password "${strong}" client-side`, () => {
        fill('Old12345!', strong);

        expect(errorText('newPassword')).toBe('');
        expect(el().querySelector<HTMLButtonElement>('#passwordForm button[type="submit"]')!.disabled).toBeFalse();
      });
    }

    it('rejects a confirmation that differs from the new password', () => {
      fill('Old12345!', 'New12345!', 'New12345?');
      submitPasswordForm();

      backend.expectNone(changePasswordUrl);
      expect(errorText('confirmNewPassword')).toContain('新密碼與確認新密碼不一致');
      expect(errorText('confirmNewPassword')).toContain('do not match');
      expect(errorText('newPassword')).toBe('');
    });

    it('clears the mismatch once the confirmation is corrected', () => {
      fill('Old12345!', 'New12345!', 'New12345?');
      expect(errorText('confirmNewPassword')).toContain('不一致');

      typePassword('confirmNewPassword', 'New12345!');

      expect(errorText('confirmNewPassword')).toBe('');
    });

    it('keeps the submit button disabled while the form is invalid', () => {
      const button = () => el().querySelector<HTMLButtonElement>('#passwordForm button[type="submit"]')!;
      expect(button().disabled).toBeTrue();

      fill('Old12345!', 'New12345!', 'nope');
      expect(button().disabled).toBeTrue();

      typePassword('confirmNewPassword', 'New12345!');
      expect(button().disabled).toBeFalse();
    });

    it('POSTs exactly { currentPassword, newPassword, confirmNewPassword } and clears the form on success', () => {
      fill('Old12345!', 'New12345!');
      submitPasswordForm();

      const req = backend.expectOne(changePasswordUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ currentPassword: 'Old12345!', newPassword: 'New12345!', confirmNewPassword: 'New12345!' });
      expect(Object.keys(req.request.body as object).sort()).toEqual(['confirmNewPassword', 'currentPassword', 'newPassword']);
      req.flush(null, { status: 204, statusText: 'No Content' });
      fixture.detectChanges();

      expect(passwordInput('currentPassword').value).toBe('');
      expect(passwordInput('newPassword').value).toBe('');
      expect(passwordInput('confirmNewPassword').value).toBe('');
      expect(messages.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success', summary: '密碼已變更' }));
    });

    it('signs the user out and returns to /login with the password-changed reason on success', () => {
      fill('Old12345!', 'New12345!');
      submitPasswordForm();
      backend.expectOne(changePasswordUrl).flush(null, { status: 204, statusText: 'No Content' });
      fixture.detectChanges();

      // The old token is rejected by the API from now on, so nothing of the session may survive.
      expect(auth.isAuthenticated()).toBeFalse();
      expect(auth.accessToken()).toBeNull();
      expect(sessionStorage.getItem(AUTH_PROFILE_KEY)).toBeNull();
      expect(navigate).toHaveBeenCalledWith([LOGIN_PATH], { queryParams: { reason: PASSWORD_CHANGED_REASON } });
    });

    it('keeps the session when the change fails', () => {
      const seeded = JSON.parse(sessionStorage.getItem(AUTH_PROFILE_KEY)!) as UserProfile;
      fill('wrong-one!', 'New12345!');
      submitPasswordForm();
      backend.expectOne(changePasswordUrl).flush(
        { errors: { CurrentPassword: ['目前密碼錯誤。'] } },
        { status: 400, statusText: 'Bad Request' }
      );
      fixture.detectChanges();

      expect(auth.isAuthenticated()).toBeTrue();
      expect(JSON.parse(sessionStorage.getItem(AUTH_PROFILE_KEY)!)).toEqual(seeded);
      expect(navigate).not.toHaveBeenCalled();
    });

    it('shows the API message under 目前密碼 when the server rejects the current password', () => {
      fill('wrong-one!', 'New12345!');
      submitPasswordForm();
      backend.expectOne(changePasswordUrl).flush(
        { errors: { CurrentPassword: ['目前密碼錯誤。'] } },
        { status: 400, statusText: 'Bad Request' }
      );
      fixture.detectChanges();

      expect(errorText('currentPassword')).toContain('目前密碼錯誤');
      expect(passwordInput('newPassword').value).toBe('New12345!'); // nothing is cleared on failure
      expect(messages.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error', detail: '目前密碼錯誤。' }));
    });

    it('shows the bilingual rule under 新密碼 when the server rejects it on policy', () => {
      fill('Old12345!', 'Abcdefg1');
      submitPasswordForm();
      backend.expectOne(changePasswordUrl).flush(
        { errors: { NewPassword: ['密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號'] } },
        { status: 400, statusText: 'Bad Request' }
      );
      fixture.detectChanges();

      expect(errorText('newPassword')).toContain('uppercase / lowercase / digit / symbol');
    });

    it('shows a generic toast for an unexpected failure', () => {
      fill('Old12345!', 'New12345!');
      submitPasswordForm();
      backend.expectOne(changePasswordUrl).flush(null, { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();

      expect(messages.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error', detail: '變更密碼失敗，請稍後再試。' }));
    });
  });
});
