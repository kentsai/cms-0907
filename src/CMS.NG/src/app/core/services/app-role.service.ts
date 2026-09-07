import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { AppRole, AppRoleQuery, AppRoleRequest } from '@core/models/app-role.model';

@Injectable({ providedIn: 'root' })
export class AppRoleService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/app-roles`;

  getAll(): Observable<AppRole[]> {
    return this.http.get<AppRole[]>(this.baseUrl);
  }

  query(query: AppRoleQuery): Observable<AppRole[]> {
    return this.http.post<AppRole[]>(`${this.baseUrl}/query`, query);
  }

  /** String PK: the RoleId is URL-encoded. */
  getById(roleId: string): Observable<AppRole> {
    return this.http.get<AppRole>(`${this.baseUrl}/${encodeURIComponent(roleId)}`);
  }

  create(request: AppRoleRequest): Observable<AppRole> {
    return this.http.post<AppRole>(this.baseUrl, request);
  }

  /** PUT takes the RoleId from the body — no route parameter. */
  update(request: AppRoleRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  /** String PK: the RoleId is URL-encoded. */
  delete(roleId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(roleId)}`);
  }
}
