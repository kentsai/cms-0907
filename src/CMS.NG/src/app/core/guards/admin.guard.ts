import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

/** Where a signed-in non-administrator is sent when they aim at an admin-only route. */
export const ADMIN_FALLBACK_PATH = '/home/featured-promo-items';

/**
 * Keeps the 系統管理 account and role pages to administrators, matching the API's `Admin` policy on
 * `AppUsersController`, `AppRolesController` and the two account/role lookups. The API is the control —
 * a non-administrator who calls those endpoints directly gets **403** whatever the browser does — and this
 * guard is the matching user experience: without it the sidebar would still hide the menu, but a pasted or
 * bookmarked URL would open a page whose every request fails.
 *
 * Runs after `authGuard` on the same route group, so an unauthenticated visitor is sent to `/login` (and a
 * default-password session to `/change-password`) before this ever applies.
 */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAdmin() ? true : inject(Router).createUrlTree([ADMIN_FALLBACK_PATH]);
};
