import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { CourseGroup, CourseGroupQuery, CourseGroupRequest } from '@core/models/course-group.model';

@Injectable({ providedIn: 'root' })
export class CourseGroupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/course-groups`;

  getAll(): Observable<CourseGroup[]> {
    return this.http.get<CourseGroup[]>(this.baseUrl);
  }

  query(query: CourseGroupQuery): Observable<CourseGroup[]> {
    return this.http.post<CourseGroup[]>(`${this.baseUrl}/query`, query);
  }

  getById(id: number): Observable<CourseGroup> {
    return this.http.get<CourseGroup>(`${this.baseUrl}/${id}`);
  }

  /** Returns the created row including its server-assigned pkid. */
  create(request: CourseGroupRequest): Observable<CourseGroup> {
    return this.http.post<CourseGroup>(this.baseUrl, request);
  }

  /** PUT takes the pkid from the body — no route parameter. */
  update(request: CourseGroupRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
