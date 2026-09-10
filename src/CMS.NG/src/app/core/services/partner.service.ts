import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { Partner, PartnerQuery, PartnerRequest } from '@core/models/partner.model';

@Injectable({ providedIn: 'root' })
export class PartnerService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/partners`;

  getAll(): Observable<Partner[]> {
    return this.http.get<Partner[]>(this.baseUrl);
  }

  query(query: PartnerQuery): Observable<Partner[]> {
    return this.http.post<Partner[]>(`${this.baseUrl}/query`, query);
  }

  getById(id: number): Observable<Partner> {
    return this.http.get<Partner>(`${this.baseUrl}/${id}`);
  }

  /** Returns the created row including its server-assigned pkid. */
  create(request: PartnerRequest): Observable<Partner> {
    return this.http.post<Partner>(this.baseUrl, request);
  }

  /** PUT takes the pkid from the body — no route parameter. */
  update(request: PartnerRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
