import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '@environments/environment';
import {
  ChangePasswordRequest,
  LoginRequest,
  ProfileResponse,
  UpdateProfileRequest,
  UserProfile
} from '@core/models/auth.model';
import { rolesFromToken } from '@core/utils/jwt.util';
import { readSession, writeSession } from '@core/utils/session-storage.util';

/** sessionStorage key holding the `UserProfile` of the signed-in user. */
export const AUTH_PROFILE_KEY = 'auth-profile';
/** The public login route; the guard and the 401 handler both send the user here. */
export const LOGIN_PATH = '/login';
/** `?reason=` value the profile page sends to `/login` after a password change, so the page can say why. */
export const PASSWORD_CHANGED_REASON = 'password-changed';
/** Role that unlocks the `系統管理 Admin` menu group. */
export const ADMIN_ROLE = 'Admin';

/**
 * Sign-in state for the whole app. The profile lives in **session** storage (never local storage), so
 * it is gone when the tab closes; the signals mirror it so the shell reacts to login / logout.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly loginUrl = `${environment.apiBaseUrl}/auth/login`;
  readonly profileUrl = `${environment.apiBaseUrl}/auth/profile`;
  readonly changePasswordUrl = `${environment.apiBaseUrl}/auth/change-password`;

  private readonly profileState = signal<UserProfile | null>(readStoredProfile());

  readonly profile = this.profileState.asReadonly();
  readonly isAuthenticated = computed(() => this.profileState() !== null);
  readonly userName = computed(() => this.profileState()?.userName ?? '');
  /** Roles carried as claims in the stored token — no extra API call. */
  readonly roles = computed(() => rolesFromToken(this.profileState()?.accessToken));
  readonly isAdmin = computed(() => this.roles().includes(ADMIN_ROLE));

  /** Posts the credentials and, on success, stores the returned profile in session storage. */
  login(request: LoginRequest): Observable<UserProfile> {
    return this.http.post<UserProfile>(this.loginUrl, request).pipe(tap(profile => this.storeProfile(profile)));
  }

  /**
   * PUT `/auth/profile` with the new UserName only; the API takes the user from the token. On success the
   * stored profile (session storage + signals, hence the shell) is refreshed with the returned UserName.
   */
  updateProfile(userName: string): Observable<ProfileResponse> {
    return this.http
      .put<ProfileResponse>(this.profileUrl, { userName } satisfies UpdateProfileRequest)
      .pipe(
        tap(updated => {
          const current = this.profileState();
          if (current) {
            this.storeProfile({ ...current, userName: updated.userName });
          }
        })
      );
  }

  /**
   * POST `/auth/change-password`. The API verifies the current password and the policy for the token's user
   * and answers 204 or a 400 `ValidationProblem` keyed by field. On 204 the current token is dead server-side
   * (issued before the new `PasswordUpdatedTime`), so the caller must `clear()` and send the user to `/login`.
   */
  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http.post<void>(this.changePasswordUrl, request);
  }

  /**
   * The token as currently held in session storage (not the signal), so the interceptor and the guard
   * see exactly what is stored — including a clear done elsewhere.
   */
  accessToken(): string | null {
    return readStoredProfile()?.accessToken ?? null;
  }

  storeProfile(profile: UserProfile): void {
    writeSession(AUTH_PROFILE_KEY, profile);
    this.profileState.set(profile);
  }

  /** Forgets the user: clears the whole session storage (profile and remembered list filters). */
  clear(): void {
    try {
      sessionStorage.clear();
    } catch {
      // storage unavailable – nothing to clear
    }
    this.profileState.set(null);
  }

  /** Clears the session and returns to the login page. */
  logout(): Promise<boolean> {
    this.clear();
    return this.router.navigateByUrl(LOGIN_PATH);
  }
}

function readStoredProfile(): UserProfile | null {
  const stored = readSession<Partial<UserProfile> | null>(AUTH_PROFILE_KEY, null);
  return stored && typeof stored.accessToken === 'string' && stored.accessToken.length > 0
    ? { userId: stored.userId ?? '', userName: stored.userName ?? '', accessToken: stored.accessToken }
    : null;
}
