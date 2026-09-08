import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '@core/services/auth.service';

/**
 * Attaches `Authorization: Bearer <token>` (read from session storage) to every outgoing request and,
 * when the API answers 401, clears the session and returns to the login page. A 401 from the login
 * endpoint itself means wrong credentials, so it is left for the login page to display.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken();
  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && req.url !== auth.loginUrl) {
        auth.logout();
      }
      return throwError(() => error);
    })
  );
};
