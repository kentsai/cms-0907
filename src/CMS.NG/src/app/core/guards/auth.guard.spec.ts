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

  function run(): GuardResult {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
    const state = { url: attemptedUrl } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, state)) as GuardResult;
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('redirects to the login page (with returnUrl) when session storage has no token', () => {
    const result = run();

    expect(result).toBeInstanceOf(UrlTree);
    const tree = result as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(tree)).toBe(`/login?returnUrl=${encodeURIComponent(attemptedUrl)}`);
  });

  it('allows the route when a token is stored', () => {
    seedSignedInUser();

    expect(run()).toBeTrue();
  });
});
