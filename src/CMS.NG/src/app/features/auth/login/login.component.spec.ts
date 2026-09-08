import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { AUTH_PROFILE_KEY } from '@core/services/auth.service';
import { fakeProfile } from '@app/testing/auth-testing';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  const loginUrl = `${environment.apiBaseUrl}/auth/login`;
  let fixture: ComponentFixture<LoginComponent>;
  let backend: HttpTestingController;
  let navigate: jasmine.Spy;

  async function mount(queryParams: Record<string, string> = {}): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        MessageService,
        ConfirmationService,
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    }).compileComponents();

    backend = TestBed.inject(HttpTestingController);
    navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
  }

  function fillAndSubmit(userId = 'mei', password = 'secret'): void {
    const el = fixture.nativeElement as HTMLElement;
    const userInput = el.querySelector<HTMLInputElement>('#userId')!;
    const passwordInput = el.querySelector<HTMLInputElement>('#password')!;
    userInput.value = userId;
    userInput.dispatchEvent(new Event('input'));
    passwordInput.value = password;
    passwordInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

  afterEach(() => {
    backend.verify();
    sessionStorage.clear();
  });

  it('posts { userId, password } to /auth/login, stores the profile in session storage and navigates to /', async () => {
    await mount();
    const profile = fakeProfile(['Admin']);

    fillAndSubmit('mei', 'secret');

    const req = backend.expectOne(loginUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'mei', password: 'secret' });
    req.flush(profile);

    expect(JSON.parse(sessionStorage.getItem(AUTH_PROFILE_KEY)!)).toEqual(profile);
    expect(localStorage.length).toBe(0);
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('navigates to an in-app returnUrl after login', async () => {
    await mount({ returnUrl: '/course/courses' });

    fillAndSubmit();
    backend.expectOne(loginUrl).flush(fakeProfile());

    expect(navigate).toHaveBeenCalledWith('/course/courses');
  });

  it('ignores an external returnUrl', async () => {
    await mount({ returnUrl: 'https://evil.example/phish' });

    fillAndSubmit();
    backend.expectOne(loginUrl).flush(fakeProfile());

    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('shows the server message on 401 and stores nothing', async () => {
    await mount();

    fillAndSubmit('mei', 'wrong');
    backend.expectOne(loginUrl).flush({ message: '帳號或密碼錯誤。' }, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('帳號或密碼錯誤。');
    expect(sessionStorage.getItem(AUTH_PROFILE_KEY)).toBeNull();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('shows a generic message on other errors', async () => {
    await mount();

    fillAndSubmit();
    backend.expectOne(loginUrl).flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('登入失敗');
  });

  it('shows the password-changed notice when sent here after a password change', async () => {
    await mount({ reason: 'password-changed' });

    const notice = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="login-notice"]');
    expect(notice?.textContent).toContain('密碼已變更，請使用新密碼重新登入');
    expect(notice?.textContent).toContain('sign in again with the new password');
  });

  it('shows no notice by default or for an unknown reason', async () => {
    await mount({ reason: 'something-else' });

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="login-notice"]')).toBeNull();
  });

  it('still lands on / after logging in with the password-changed reason', async () => {
    await mount({ reason: 'password-changed' });

    fillAndSubmit();
    backend.expectOne(loginUrl).flush(fakeProfile());

    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('does not call the API while the form is empty', async () => {
    await mount();

    (fixture.nativeElement as HTMLElement).querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    backend.expectNone(loginUrl);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('請輸入帳號');
  });
});
