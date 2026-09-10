import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { AuthService, CHANGE_PASSWORD_PATH } from '@core/services/auth.service';

/** Toast title for any 5xx answer. */
export const SERVER_ERROR_SUMMARY = '系統錯誤 Server Error';
/** Shown when a 5xx answer carries no usable `message` (a proxy page, an empty body, a network-level failure). */
export const SERVER_ERROR_FALLBACK = '系統發生未預期的錯誤，請稍後再試。';

/** The safe `message` the API's GlobalExceptionHandler puts in every 500 body, or the fallback when there is none. */
export function serverErrorDetail(error: HttpErrorResponse): string {
  const message: unknown = (error.error as { message?: unknown } | null)?.message;
  return typeof message === 'string' && message.trim() !== '' ? message : SERVER_ERROR_FALLBACK;
}

/**
 * Attaches `Authorization: Bearer <token>` (read from session storage) to every outgoing request and turns the
 * API's status codes into app behaviour:
 * - **5xx** (any URL, the login endpoint included): the API has already logged the real exception and answered
 *   with a generic `{ message, traceId }`; that message is shown in the app-wide toast. The error is still
 *   re-thrown, so a page may additionally react (a form stays editable, a list shows its own state).
 * - **401** (except from the login endpoint, where it means wrong credentials): clears the session and returns to
 *   the login page.
 * - **403** while the session still has to change its password (the API refuses everything else): sends the user
 *   to the change-password page without ending the session; the guard normally prevents such calls.
 * - Everything else (400 validation, 404, 409): untouched — the page that made the call handles it.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const messages = inject(MessageService);
  const token = auth.accessToken();
  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        if (error.status >= 500) {
          messages.add({ severity: 'error', summary: SERVER_ERROR_SUMMARY, detail: serverErrorDetail(error) });
        } else if (req.url !== auth.loginUrl) {
          if (error.status === 401) {
            auth.logout();
          } else if (error.status === 403 && auth.mustChangePassword()) {
            void router.navigateByUrl(CHANGE_PASSWORD_PATH);
          }
        }
      }
      return throwError(() => error);
    })
  );
};
