import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, CHANGE_PASSWORD_PATH, LOGIN_PATH } from '@core/services/auth.service';

/**
 * Blocks app routes when there is no token in session storage, redirecting to the login page with
 * the attempted URL as `returnUrl`. A session that signed in with the default password may only reach
 * the change-password page (the API answers 403 everywhere else), so every other URL redirects there.
 * Used as `canActivateChild` on the guarded route group.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (!auth.accessToken()) {
    return inject(Router).createUrlTree([LOGIN_PATH], { queryParams: { returnUrl: state.url } });
  }
  if (auth.mustChangePassword() && !isChangePasswordUrl(state.url)) {
    return inject(Router).createUrlTree([CHANGE_PASSWORD_PATH]);
  }
  return true;
};

/** The change-password page itself, with or without a query string. */
function isChangePasswordUrl(url: string): boolean {
  const path = url.split('?')[0].split('#')[0];
  return path === CHANGE_PASSWORD_PATH;
}
