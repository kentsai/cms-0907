import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import {
  FeaturedPromoItem,
  FeaturedPromoItemRequest,
  FeaturedPromoWeek
} from '@core/models/featured-promo-item.model';

@Injectable({ providedIn: 'root' })
export class FeaturedPromoItemService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/featured-promo-items`;

  getAll(): Observable<FeaturedPromoItem[]> {
    return this.http.get<FeaturedPromoItem[]>(this.baseUrl);
  }

  /**
   * Items of one training centre for the Monday–Sunday week containing `date` (`'yyyy-MM-dd'`).
   * The server snaps the date to the week, so any day of the week yields the same result.
   */
  getWeek(trainingCenterPkid: number, date: string): Observable<FeaturedPromoWeek> {
    const params = new HttpParams()
      .set('trainingCenterPkid', trainingCenterPkid)
      .set('date', date);
    return this.http.get<FeaturedPromoWeek>(`${this.baseUrl}/week`, { params });
  }

  getById(id: number): Observable<FeaturedPromoItem> {
    return this.http.get<FeaturedPromoItem>(`${this.baseUrl}/${id}`);
  }

  /** Returns the created row (re-read by the API, so it includes the JOINed PromoCode and pkid). */
  create(request: FeaturedPromoItemRequest): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(this.baseUrl, request);
  }

  /** PUT takes the pkid from the body — no route parameter. */
  update(request: FeaturedPromoItemRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Slot n → n-1, swapping with the occupant when there is one. */
  moveUp(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/move-up`, null);
  }

  /** Slot n → n+1, swapping with the occupant when there is one. */
  moveDown(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/move-down`, null);
  }
}
