import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { RowAudit } from '@core/models/row-audit.model';

@Injectable({ providedIn: 'root' })
export class RowAuditService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/row-audits`;

  /** Audit rows for one record, newest first. */
  getForRow(tableName: string, primaryKeyValues: string | number, take = 20): Observable<RowAudit[]> {
    const url = `${this.baseUrl}/${encodeURIComponent(tableName)}/${encodeURIComponent(String(primaryKeyValues))}`;
    return this.http.get<RowAudit[]>(url, { params: new HttpParams().set('take', take) });
  }
}
