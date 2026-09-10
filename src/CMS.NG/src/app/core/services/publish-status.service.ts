import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { PublishStatus, PublishStatusQuery, PublishStatusRequest } from '@core/models/publish-status.model';

@Injectable({ providedIn: 'root' })
export class PublishStatusService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/publish-statuses`;

  getAll(): Observable<PublishStatus[]> {
    return this.http.get<PublishStatus[]>(this.baseUrl);
  }

  query(query: PublishStatusQuery): Observable<PublishStatus[]> {
    return this.http.post<PublishStatus[]>(`${this.baseUrl}/query`, query);
  }

  getById(id: number): Observable<PublishStatus> {
    return this.http.get<PublishStatus>(`${this.baseUrl}/${id}`);
  }

  create(request: PublishStatusRequest): Observable<PublishStatus> {
    return this.http.post<PublishStatus>(this.baseUrl, request);
  }

  /** PUT takes the pkid from the body — no route parameter. */
  update(request: PublishStatusRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
