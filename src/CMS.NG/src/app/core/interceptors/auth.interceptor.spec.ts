import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { AUTH_PROFILE_KEY, CHANGE_PASSWORD_PATH, LOGIN_PATH } from '@core/services/auth.service';
import { seedSignedInUser } from '@app/testing/auth-testing';
import { SERVER_ERROR_FALLBACK, SERVER_ERROR_SUMMARY, authInterceptor, serverErrorDetail } from './auth.interceptor';

describe('authInterceptor', () => {
  const apiUrl = `${environment.apiBaseUrl}/publish-statuses`;
  const loginUrl = `${environment.apiBaseUrl}/auth/login`;
  let http: HttpClient;
  let backend: HttpTestingController;
  let navigate: jasmine.Spy;
  let toast: jasmine.Spy;
  /** True once a spec has called `setup()`; the pure `serverErrorDetail` specs never do. */
  let configured = false;

  function setup(): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        MessageService
      ]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    toast = spyOn(TestBed.inject(MessageService), 'add');
    configured = true;
  }

  beforeEach(() => {
    configured = false;
    sessionStorage.clear();
  });

  afterEach(() => {
    // `serverErrorDetail` is a pure function, so those specs configure no TestBed and leave no backend to verify.
    // Jasmine randomises order, so without this guard they fail whenever they happen to run first.
    if (configured) {
      backend.verify();
    }
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
    expect(toast).not.toHaveBeenCalled();
  });

  it('leaves the session alone on other errors', () => {
    seedSignedInUser();
    setup();

    http.get(apiUrl).subscribe({ error: () => undefined });
    backend.expectOne(apiUrl).flush(null, { status: 500, statusText: 'Server Error' });

    expect(sessionStorage.getItem(AUTH_PROFILE_KEY)).not.toBeNull();
    expect(navigate).not.toHaveBeenCalled();
  });

  describe('5xx → friendly toast', () => {
    const serverBody = { message: '系統發生未預期的錯誤，請稍後再試。', traceId: '0HN7ABC:00000001' };

    it('on 500 shows the safe message from the response body, keeps the session and re-throws', () => {
      seedSignedInUser();
      setup();
      let caught: unknown;

      http.get(apiUrl).subscribe({ error: err => (caught = err) });
      backend.expectOne(apiUrl).flush(serverBody, { status: 500, statusText: 'Internal Server Error' });

      expect(toast).toHaveBeenCalledOnceWith({ severity: 'error', summary: SERVER_ERROR_SUMMARY, detail: serverBody.message });
      expect(sessionStorage.getItem(AUTH_PROFILE_KEY)).not.toBeNull();
      expect(navigate).not.toHaveBeenCalled();
      expect((caught as HttpErrorResponse).status).toBe(500);
    });

    it('falls back to a generic message when the 5xx body has no message (proxy page, empty body)', () => {
      seedSignedInUser();
      setup();

      http.get(apiUrl).subscribe({ error: () => undefined });
      backend.expectOne(apiUrl).flush('<html>Bad Gateway</html>', { status: 502, statusText: 'Bad Gateway' });

      expect(toast).toHaveBeenCalledOnceWith(jasmine.objectContaining({ severity: 'error', detail: SERVER_ERROR_FALLBACK }));
    });

    it('never shows the raw error text, only the message field', () => {
      seedSignedInUser();
      setup();

      http.get(apiUrl).subscribe({ error: () => undefined });
      backend
        .expectOne(apiUrl)
        .flush({ message: '系統發生未預期的錯誤，請稍後再試。', detail: 'at CMS.API.Controllers...' }, { status: 500, statusText: 'Internal Server Error' });

      const detail = (toast.calls.mostRecent().args[0] as { detail: string }).detail;
      expect(detail).toBe('系統發生未預期的錯誤，請稍後再試。');
      expect(detail).not.toContain('CMS.API');
    });

    it('also toasts a 5xx from the login endpoint (it is not wrong credentials)', () => {
      setup();

      http.post(loginUrl, { userId: 'mei', password: 'x' }).subscribe({ error: () => undefined });
      backend.expectOne(loginUrl).flush(serverBody, { status: 500, statusText: 'Internal Server Error' });

      expect(toast).toHaveBeenCalledOnceWith(jasmine.objectContaining({ detail: serverBody.message }));
      expect(navigate).not.toHaveBeenCalled();
    });

    it('does not toast 4xx answers — validation errors stay with the form', () => {
      seedSignedInUser();
      setup();

      http.put(`${environment.apiBaseUrl}/auth/profile`, { userName: '' }).subscribe({ error: () => undefined });
      backend
        .expectOne(`${environment.apiBaseUrl}/auth/profile`)
        .flush({ errors: { UserName: ['請輸入姓名。'] } }, { status: 400, statusText: 'Bad Request' });
      http.delete(`${apiUrl}/1`).subscribe({ error: () => undefined });
      backend.expectOne(`${apiUrl}/1`).flush({ message: '仍被使用' }, { status: 409, statusText: 'Conflict' });
      http.get(`${apiUrl}/99`).subscribe({ error: () => undefined });
      backend.expectOne(`${apiUrl}/99`).flush(null, { status: 404, statusText: 'Not Found' });

      expect(toast).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
    });

    it('a 401 still redirects to Login instead of toasting', () => {
      seedSignedInUser();
      setup();

      http.get(apiUrl).subscribe({ error: () => undefined });
      backend.expectOne(apiUrl).flush(null, { status: 401, statusText: 'Unauthorized' });

      expect(navigate).toHaveBeenCalledWith(LOGIN_PATH);
      expect(toast).not.toHaveBeenCalled();
    });
  });

  describe('serverErrorDetail', () => {
    const response = (body: unknown) => new HttpErrorResponse({ status: 500, error: body });

    it('takes a non-blank string message', () => {
      expect(serverErrorDetail(response({ message: '伺服器忙碌中' }))).toBe('伺服器忙碌中');
    });

    it('falls back for a missing, blank, non-string or absent body', () => {
      expect(serverErrorDetail(response(null))).toBe(SERVER_ERROR_FALLBACK);
      expect(serverErrorDetail(response('text'))).toBe(SERVER_ERROR_FALLBACK);
      expect(serverErrorDetail(response({ message: '   ' }))).toBe(SERVER_ERROR_FALLBACK);
      expect(serverErrorDetail(response({ message: 42 }))).toBe(SERVER_ERROR_FALLBACK);
      expect(serverErrorDetail(response({}))).toBe(SERVER_ERROR_FALLBACK);
    });
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
