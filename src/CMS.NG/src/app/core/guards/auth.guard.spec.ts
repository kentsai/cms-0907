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
import { authGuard } from './auth.guard';

describe('authGuard', () => {
  const attemptedUrl = '/course/courses/12/edit';

  /** Runs the guard against a URL; the TestBed is configured once per test (see `beforeEach`), so this may be called repeatedly. */
  function run(url = attemptedUrl): GuardResult {
    const state = { url } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, state)) as GuardResult;
  }

  const serialized = (result: GuardResult) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
  });
  afterEach(() => sessionStorage.clear());

  it('redirects to the login page (with returnUrl) when session storage has no token', () => {
    const result = run();

    expect(result).toBeInstanceOf(UrlTree);
    expect(serialized(result)).toBe(`/login?returnUrl=${encodeURIComponent(attemptedUrl)}`);
  });

  it('allows the route when a token is stored', () => {
    seedSignedInUser();

    expect(run()).toBeTrue();
  });

  it('allows the change-password page for an ordinary session too', () => {
    seedSignedInUser();

    expect(run('/change-password')).toBeTrue();
  });

  describe('when the session signed in with the default password', () => {
    beforeEach(() => seedSignedInUser(['Editor'], '王大明', true));

    it('redirects every other route to the change-password page', () => {
      for (const url of [attemptedUrl, '/', '/profile', '/home/featured-promo-items', '/change-password-not-really']) {
        const result = run(url);

        expect(result).toBeInstanceOf(UrlTree);
        expect(serialized(result)).toBe('/change-password');
      }
    });

    it('allows the change-password page itself, with or without a query string', () => {
      expect(run('/change-password')).toBeTrue();
      expect(run('/change-password?x=1')).toBeTrue();
    });

    it('still sends a signed-out visitor to the login page', () => {
      sessionStorage.clear();

      expect(serialized(run('/change-password'))).toBe(`/login?returnUrl=${encodeURIComponent('/change-password')}`);
    });
  });
});
