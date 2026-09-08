import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService, CHANGE_PASSWORD_PATH } from '@core/services/auth.service';

/**
 * Attaches `Authorization: Bearer <token>` (read from session storage) to every outgoing request and,
 * when the API answers 401, clears the session and returns to the login page. A 401 from the login
 * endpoint itself means wrong credentials, so it is left for the login page to display.
 * A 403 while the session still has to change its password (the API refuses everything else) sends the
 * user to the change-password page without ending the session; the guard normally prevents such calls.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.accessToken();
  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && req.url !== auth.loginUrl) {
        if (error.status === 401) {
          auth.logout();
        } else if (error.status === 403 && auth.mustChangePassword()) {
          void router.navigateByUrl(CHANGE_PASSWORD_PATH);
        }
      }
      return throwError(() => error);
    })
  );
};
