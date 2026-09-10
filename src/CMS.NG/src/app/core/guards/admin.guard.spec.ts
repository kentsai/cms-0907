import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  ActivatedRouteSnapshot,
  GuardResult,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter
} from '@angular/router';
import { seedSignedInUser } from '@app/testing/auth-testing';
import { ADMIN_FALLBACK_PATH, adminGuard } from './admin.guard';

describe('adminGuard', () => {
  const attemptedUrl = '/admin/app-users';

  function run(url = attemptedUrl): GuardResult {
    const state = { url } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => adminGuard({} as ActivatedRouteSnapshot, state)) as GuardResult;
  }

  const serialized = (result: GuardResult) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
  });
  afterEach(() => sessionStorage.clear());

  it('allows the route when the token carries the Admin role', () => {
    seedSignedInUser(['Admin']);

    expect(run()).toBeTrue();
  });

  it('allows it when Admin is one of several roles', () => {
    seedSignedInUser(['Editor', 'Admin']);

    expect(run()).toBeTrue();
  });

  it('redirects a signed-in non-administrator away instead of opening a page the API will refuse', () => {
    seedSignedInUser(['Editor']);

    const result = run();

    expect(result).toBeInstanceOf(UrlTree);
    expect(serialized(result)).toBe(ADMIN_FALLBACK_PATH);
  });

  it('redirects a user with no roles at all', () => {
    seedSignedInUser([]);

    expect(serialized(run())).toBe(ADMIN_FALLBACK_PATH);
  });

  it('redirects when there is no session, leaving the sign-in redirect to authGuard', () => {
    expect(serialized(run())).toBe(ADMIN_FALLBACK_PATH);
  });

  it('guards the role pages the same way as the account pages', () => {
    seedSignedInUser(['Editor']);

    expect(serialized(run('/admin/app-roles/Admin/edit'))).toBe(ADMIN_FALLBACK_PATH);
  });
});
