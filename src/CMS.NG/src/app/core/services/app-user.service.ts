import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { AppUser, AppUserQuery, AppUserRequest } from '@core/models/app-user.model';

@Injectable({ providedIn: 'root' })
export class AppUserService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/app-users`;

  getAll(): Observable<AppUser[]> {
    return this.http.get<AppUser[]>(this.baseUrl);
  }

  query(query: AppUserQuery): Observable<AppUser[]> {
    return this.http.post<AppUser[]>(`${this.baseUrl}/query`, query);
  }

  /** String PK: the UserId is URL-encoded. */
  getById(userId: string): Observable<AppUser> {
    return this.http.get<AppUser>(`${this.baseUrl}/${encodeURIComponent(userId)}`);
  }

  /** The server seeds the password from the system default; nothing password-related is sent. */
  create(request: AppUserRequest): Observable<AppUser> {
    return this.http.post<AppUser>(this.baseUrl, request);
  }

  /** PUT takes the UserId from the body — no route parameter. Never touches the password. */
  update(request: AppUserRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  /** String PK: the UserId is URL-encoded. */
  delete(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(userId)}`);
  }

  /** Resets the user's password to the system default (SysConfig.appConfig.defaultPassword). */
  resetPassword(userId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${encodeURIComponent(userId)}/reset-password`, null);
  }
}
