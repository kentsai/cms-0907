import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { LookupItem } from '@core/models/lookup-item.model';

/** One method per /api/lookups/* endpoint. Used by FK dropdowns in list filters and forms. */
@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/lookups`;

  publishStatuses(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.baseUrl}/publish-statuses`);
  }
}
