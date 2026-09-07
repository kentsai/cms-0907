import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { Course, CourseQuery, CourseRequest } from '@core/models/course.model';

@Injectable({ providedIn: 'root' })
export class CourseService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/courses`;

  getAll(): Observable<Course[]> {
    return this.http.get<Course[]>(this.baseUrl);
  }

  query(query: CourseQuery): Observable<Course[]> {
    return this.http.post<Course[]>(`${this.baseUrl}/query`, query);
  }

  getById(id: number): Observable<Course> {
    return this.http.get<Course>(`${this.baseUrl}/${id}`);
  }

  /** Returns the created row (re-read by the API, so it includes the JOINed labels and pkid). */
  create(request: CourseRequest): Observable<Course> {
    return this.http.post<Course>(this.baseUrl, request);
  }

  /** PUT takes the pkid from the body — no route parameter. */
  update(request: CourseRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
