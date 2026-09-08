import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { RowAudit } from '@core/models/row-audit.model';

@Injectable({ providedIn: 'root' })
export class RowAuditService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/row-audits`;

  /**
   * The full audit trail of one record, newest first. `pkid` is whatever the writer stored as
   * `PrimaryKeyValues`: the numeric pkid, or the string key of AppRole (`RoleId`) / AppUser (`UserId`).
   */
  getForRecord(tableName: string, pkid: string | number): Observable<RowAudit[]> {
    const params = new HttpParams().set('tableName', tableName).set('pkid', String(pkid));
    return this.http.get<RowAudit[]>(this.url, { params });
  }
}
