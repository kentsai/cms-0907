import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '@environments/environment';
import { UserProfile } from '@core/models/auth.model';
import { fakeProfile, seedSignedInUser } from '@app/testing/auth-testing';
import { AUTH_PROFILE_KEY, AuthService, LOGIN_PATH } from './auth.service';

describe('AuthService', () => {
  const loginUrl = `${environment.apiBaseUrl}/auth/login`;
  let http: HttpTestingController;
  let router: Router;

  function createService(): AuthService {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  it('starts signed out when session storage holds no profile', () => {
    const service = createService();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.accessToken()).toBeNull();
    expect(service.userName()).toBe('');
    expect(service.roles()).toEqual([]);
  });

  it('restores the profile from session storage on creation', () => {
    const stored = seedSignedInUser(['Editor'], '王大明');

    const service = createService();

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.profile()).toEqual(stored);
    expect(service.userName()).toBe('王大明');
    expect(service.accessToken()).toBe(stored.accessToken);
  });

  it('ignores a stored value without an accessToken', () => {
    sessionStorage.setItem(AUTH_PROFILE_KEY, JSON.stringify({ userId: 'x', userName: 'y' }));

    expect(createService().isAuthenticated()).toBeFalse();
  });

  it('login POSTs { userId, password } and stores the returned profile in SESSION storage only', () => {
    const service = createService();
    const profile: UserProfile = fakeProfile(['Admin']);
    let result: UserProfile | undefined;

    service.login({ userId: 'mei', password: 'secret' }).subscribe(r => (result = r));

    const req = http.expectOne(loginUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'mei', password: 'secret' });
    req.flush(profile);

    expect(result).toEqual(profile);
    expect(JSON.parse(sessionStorage.getItem(AUTH_PROFILE_KEY)!)).toEqual(profile);
    expect(localStorage.getItem(AUTH_PROFILE_KEY)).toBeNull();
    expect(localStorage.length).toBe(0);
    expect(service.isAuthenticated()).toBeTrue();
    expect(service.userName()).toBe(profile.userName);
  });

  it('reads the roles from the stored token', () => {
    seedSignedInUser(['Editor', 'Admin']);

    const service = createService();

    expect(service.roles()).toEqual(['Editor', 'Admin']);
    expect(service.isAdmin()).toBeTrue();
  });

  it('is not admin when the roles do not include Admin', () => {
    seedSignedInUser(['Editor']);

    expect(createService().isAdmin()).toBeFalse();
  });

  it('accessToken() reflects session storage, not a stale signal', () => {
    seedSignedInUser();
    const service = createService();

    sessionStorage.clear();

    expect(service.accessToken()).toBeNull();
  });

  it('updateProfile PUTs { userName } only and refreshes the stored profile, keeping the token', () => {
    const seeded = seedSignedInUser(['Admin'], '陳小美');
    const service = createService();

    service.updateProfile('陳大美').subscribe();

    const req = http.expectOne(`${environment.apiBaseUrl}/auth/profile`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ userName: '陳大美' });
    req.flush({ userId: 'mei', userName: '陳大美', roles: ['Admin'] });

    expect(service.userName()).toBe('陳大美');
    expect(service.profile()).toEqual({ ...seeded, userName: '陳大美' });
    expect(JSON.parse(sessionStorage.getItem(AUTH_PROFILE_KEY)!)).toEqual({ ...seeded, userName: '陳大美' });
    expect(service.isAdmin()).toBeTrue();
  });

  it('updateProfile leaves the stored profile alone when the API fails', () => {
    const seeded = seedSignedInUser(['Admin'], '陳小美');
    const service = createService();

    service.updateProfile('陳大美').subscribe({ error: () => undefined });
    http.expectOne(`${environment.apiBaseUrl}/auth/profile`).flush(null, { status: 500, statusText: 'Server Error' });

    expect(service.profile()).toEqual(seeded);
  });

  it('changePassword POSTs the three plain-text fields and leaves the stored profile untouched', () => {
    const seeded = seedSignedInUser(['Admin'], '陳小美');
    const service = createService();
    let completed = false;

    service
      .changePassword({ currentPassword: 'Old12345!', newPassword: 'New12345!', confirmNewPassword: 'New12345!' })
      .subscribe({ complete: () => (completed = true) });

    const req = http.expectOne(`${environment.apiBaseUrl}/auth/change-password`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ currentPassword: 'Old12345!', newPassword: 'New12345!', confirmNewPassword: 'New12345!' });
    expect(Object.keys(req.request.body as object)).not.toContain('userId');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(completed).toBeTrue();
    expect(service.profile()).toEqual(seeded);
    expect(JSON.parse(sessionStorage.getItem(AUTH_PROFILE_KEY)!)).toEqual(seeded);
  });

  it('logout clears session storage entirely and navigates to the login page', async () => {
    seedSignedInUser();
    sessionStorage.setItem('course-list-filters', '{"keyword":"x"}');
    const service = createService();
    const navigate = spyOn(router, 'navigateByUrl').and.resolveTo(true);

    await service.logout();

    expect(sessionStorage.length).toBe(0);
    expect(service.isAuthenticated()).toBeFalse();
    expect(navigate).toHaveBeenCalledWith(LOGIN_PATH);
  });
});
