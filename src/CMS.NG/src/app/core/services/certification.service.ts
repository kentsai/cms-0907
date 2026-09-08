import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { Certification, CertificationQuery, CertificationRequest } from '@core/models/certification.model';

@Injectable({ providedIn: 'root' })
export class CertificationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/certifications`;

  getAll(): Observable<Certification[]> {
    return this.http.get<Certification[]>(this.baseUrl);
  }

  query(query: CertificationQuery): Observable<Certification[]> {
    return this.http.post<Certification[]>(`${this.baseUrl}/query`, query);
  }

  getById(id: number): Observable<Certification> {
    return this.http.get<Certification>(`${this.baseUrl}/${id}`);
  }

  /** Returns the created row (re-read by the API, so it includes the JOINed partner name and pkid). */
  create(request: CertificationRequest): Observable<Certification> {
    return this.http.post<Certification>(this.baseUrl, request);
  }

  /** PUT takes the pkid from the body — no route parameter. */
  update(request: CertificationRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
