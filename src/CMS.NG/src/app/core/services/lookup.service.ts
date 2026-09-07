import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { LookupItem, StringLookupItem } from '@core/models/lookup-item.model';

/** One method per /api/lookups/* endpoint. Used by FK dropdowns in list filters and forms. */
@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/lookups`;

  publishStatuses(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.baseUrl}/publish-statuses`);
  }

  /** pkid = Partner.pkid, label = Name; ordered by DisplayOrder then Name. */
  partners(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.baseUrl}/partners`);
  }

  /** id = RoleId, label = RoleName. */
  appRoles(): Observable<StringLookupItem[]> {
    return this.http.get<StringLookupItem[]>(`${this.baseUrl}/app-roles`);
  }

  /** id = UserId, label = "UserName (UserId)". */
  appUsers(): Observable<StringLookupItem[]> {
    return this.http.get<StringLookupItem[]>(`${this.baseUrl}/app-users`);
  }
}
