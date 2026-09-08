import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '@environments/environment';
import { AUTH_PROFILE_KEY, CHANGE_PASSWORD_PATH, LOGIN_PATH } from '@core/services/auth.service';
import { seedSignedInUser } from '@app/testing/auth-testing';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  const apiUrl = `${environment.apiBaseUrl}/publish-statuses`;
  const loginUrl = `${environment.apiBaseUrl}/auth/login`;
  let http: HttpClient;
  let backend: HttpTestingController;
  let navigate: jasmine.Spy;

  function setup(): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
  }

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    backend.verify();
    sessionStorage.clear();
  });

  it('attaches the session-storage token as a Bearer header', () => {
    const { accessToken } = seedSignedInUser();
    setup();

    http.get(apiUrl).subscribe();

    const req = backend.expectOne(apiUrl);
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${accessToken}`);
    req.flush([]);
  });

  it('sends no Authorization header when there is no token', () => {
    setup();

    http.get(apiUrl).subscribe();

    const req = backend.expectOne(apiUrl);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush([]);
  });

  it('on 401 clears session storage, redirects to the login page and re-throws the error', () => {
    seedSignedInUser();
    sessionStorage.setItem('course-list-filters', '{}');
    setup();
    let caught: unknown;

    http.get(apiUrl).subscribe({ error: err => (caught = err) });
    backend.expectOne(apiUrl).flush({ message: 'nope' }, { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem(AUTH_PROFILE_KEY)).toBeNull();
    expect(sessionStorage.length).toBe(0);
    expect(navigate).toHaveBeenCalledWith(LOGIN_PATH);
    expect(caught).toBeInstanceOf(HttpErrorResponse);
    expect((caught as HttpErrorResponse).status).toBe(401);
  });

  it('leaves the session alone on other errors', () => {
    seedSignedInUser();
    setup();

    http.get(apiUrl).subscribe({ error: () => undefined });
    backend.expectOne(apiUrl).flush(null, { status: 500, statusText: 'Server Error' });

    expect(sessionStorage.getItem(AUTH_PROFILE_KEY)).not.toBeNull();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('on 403 for a default-password session goes to the change-password page but keeps the session', () => {
    seedSignedInUser(['Editor'], '王大明', true);
    setup();
    let caught: unknown;

    http.get(apiUrl).subscribe({ error: err => (caught = err) });
    backend.expectOne(apiUrl).flush({ message: '請先變更密碼後再使用系統。' }, { status: 403, statusText: 'Forbidden' });

    expect(sessionStorage.getItem(AUTH_PROFILE_KEY)).not.toBeNull();
    expect(navigate).toHaveBeenCalledWith(CHANGE_PASSWORD_PATH);
    expect((caught as HttpErrorResponse).status).toBe(403);
  });

  it('leaves a 403 alone for an ordinary session', () => {
    seedSignedInUser();
    setup();

    http.get(apiUrl).subscribe({ error: () => undefined });
    backend.expectOne(apiUrl).flush(null, { status: 403, statusText: 'Forbidden' });

    expect(sessionStorage.getItem(AUTH_PROFILE_KEY)).not.toBeNull();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('still signs a default-password session out on 401', () => {
    seedSignedInUser(['Editor'], '王大明', true);
    setup();

    http.get(apiUrl).subscribe({ error: () => undefined });
    backend.expectOne(apiUrl).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem(AUTH_PROFILE_KEY)).toBeNull();
    expect(navigate).toHaveBeenCalledWith(LOGIN_PATH);
  });

  it('does not treat a 401 from the login endpoint as an expired session', () => {
    setup();
    let status: number | undefined;

    http.post(loginUrl, { userId: 'mei', password: 'wrong' }).subscribe({
      error: (err: HttpErrorResponse) => (status = err.status)
    });
    backend.expectOne(loginUrl).flush({ message: '帳號或密碼錯誤。' }, { status: 401, statusText: 'Unauthorized' });

    expect(status).toBe(401);
    expect(navigate).not.toHaveBeenCalled();
  });
});
