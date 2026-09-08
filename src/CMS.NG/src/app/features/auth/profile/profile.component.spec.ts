import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { AUTH_PROFILE_KEY, AuthService } from '@core/services/auth.service';
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

  describe('變更密碼 Change Password card', () => {
    // The form's own behaviour is covered by change-password-form.component.spec.ts.
    beforeEach(async () => {
      seedSignedInUser(['Admin'], '陳小美');
      await mount();
    });

    it('hosts the shared change-password form with its three masked inputs', () => {
      expect(el().textContent).toContain('變更密碼 Change Password');
      expect(el().querySelector('app-change-password-form')).not.toBeNull();

      const inputs = el().querySelectorAll<HTMLInputElement>('#passwordForm input');
      expect(Array.from(inputs, i => i.id)).toEqual(['currentPassword', 'newPassword', 'confirmNewPassword']);
      expect(Array.from(inputs).every(i => i.type === 'password')).toBeTrue();
    });
  });
});
