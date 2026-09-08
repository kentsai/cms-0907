import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, LOGIN_PATH } from '@core/services/auth.service';

/**
 * Blocks app routes when there is no token in session storage, redirecting to the login page with
 * the attempted URL as `returnUrl`. Used as `canActivateChild` on the guarded route group.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  if (inject(AuthService).accessToken()) {
    return true;
  }
  return inject(Router).createUrlTree([LOGIN_PATH], { queryParams: { returnUrl: state.url } });
};
