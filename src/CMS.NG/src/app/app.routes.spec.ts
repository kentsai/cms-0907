import { Route, Routes } from '@angular/router';
import { adminGuard } from '@core/guards/admin.guard';
import { authGuard } from '@core/guards/auth.guard';
import { routes } from './app.routes';

/**
 * The guards themselves are covered by `admin.guard.spec.ts` / `auth.guard.spec.ts`; what nothing covered
 * was the wiring — *which* routes carry them. A page whose API refuses non-administrators but whose route
 * forgets `adminGuard` opens a shell that 403s on every request, and a page the menu hides but the route
 * leaves open is reachable by anyone who pastes the URL.
 *
 * Regression: ISSUE-004 — /admin/publish-statuses was reachable by any signed-in user
 * Found by /qa on 2026-09-10
 * Report: .gstack/qa-reports/qa-report-localhost-2026-09-10.md
 */
describe('app routes', () => {
  /** The `path: ''` group that `authGuard` wraps; every signed-in page lives under it. */
  const signedInGroup = routes.find(route => route.path === '' && route.children !== undefined)!;
  const featureRoutes: Routes = signedInGroup.children ?? [];

  const routeFor = (path: string): Route =>
    featureRoutes.find(route => route.path === path) ??
    fail(`no route declared for '${path}'`) as never;

  const guards = (path: string) => routeFor(path).canActivate ?? [];

  /** Every route the API answers with 403 unless the token carries the Admin role. */
  const ADMIN_ONLY_PATHS = ['admin/app-users', 'admin/app-roles', 'admin/publish-statuses'];

  /** Signed-in pages any role may open — the API asks only for a valid token. */
  const OPEN_PATHS = [
    'home/featured-promo-items',
    'course/partners',
    'course/course-groups',
    'course/courses',
    'course/certifications'
  ];

  it('wraps every signed-in page in authGuard', () => {
    expect(signedInGroup.canActivateChild).toContain(authGuard);
  });

  ADMIN_ONLY_PATHS.forEach(path => {
    it(`guards /${path} with adminGuard, matching the API's Admin policy`, () => {
      expect(guards(path)).toContain(adminGuard);
    });
  });

  OPEN_PATHS.forEach(path => {
    it(`leaves /${path} open to any signed-in user`, () => {
      expect(guards(path)).not.toContain(adminGuard);
    });
  });

  it('guards exactly the admin paths and no others', () => {
    const guarded = featureRoutes
      .filter(route => (route.canActivate ?? []).includes(adminGuard))
      .map(route => route.path);

    expect(guarded.sort()).toEqual([...ADMIN_ONLY_PATHS].sort());
  });

  /**
   * Regression: ISSUE-006 — an unmatched URL left the router outlet empty
   * Found by /qa on 2026-09-10
   * Report: .gstack/qa-reports/qa-report-localhost-2026-09-10-r2.md
   *
   * Without a `**` route Angular threw NG04002 and rendered the shell around a blank page. The
   * wildcard must stay INSIDE the authGuard group and LAST: outside it an unmatched URL would skip
   * the sign-in redirect, and anywhere but last it would swallow the routes declared after it.
   */
  describe('unmatched URLs (ISSUE-006)', () => {
    it('declares a ** wildcard that redirects home', () => {
      const wildcard = featureRoutes.find(route => route.path === '**');

      expect(wildcard).toBeDefined();
      expect(wildcard!.redirectTo).toBe('home/featured-promo-items');
    });

    it('keeps the wildcard inside the authGuard group so signed-out users still reach /login', () => {
      expect(routes.some(route => route.path === '**')).toBeFalse();
      expect(signedInGroup.canActivateChild).toContain(authGuard);
    });

    it('keeps the wildcard last so it cannot swallow a real route', () => {
      expect(featureRoutes[featureRoutes.length - 1].path).toBe('**');
    });

    it('redirects to a path that is itself a declared route', () => {
      const target = featureRoutes.find(route => route.path === '**')!.redirectTo;

      expect(featureRoutes.some(route => route.path === target)).toBeTrue();
    });
  });
});
