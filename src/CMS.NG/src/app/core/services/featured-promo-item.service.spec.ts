import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { FeaturedPromoItem, FeaturedPromoItemRequest, FeaturedPromoWeek } from '@core/models/featured-promo-item.model';
import { FeaturedPromoItemService } from './featured-promo-item.service';

describe('FeaturedPromoItemService', () => {
  const baseUrl = `${environment.apiBaseUrl}/featured-promo-items`;
  let service: FeaturedPromoItemService;
  let http: HttpTestingController;

  const sample: FeaturedPromoItem = {
    pkid: 1,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 50,
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
    trainingCenterName: '台北',
    promoCode: '20251204_SkillTrainAI'
  };

  const request: FeaturedPromoItemRequest = {
    pkid: 0,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 50,
    topic: sample.topic,
    description: sample.description
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(FeaturedPromoItemService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getAll GETs the collection', () => {
    let result: FeaturedPromoItem[] | undefined;
    service.getAll().subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);

    expect(result).toEqual([sample]);
  });

  it('getWeek GETs /week with the training centre and date as query params', () => {
    let result: FeaturedPromoWeek | undefined;
    service.getWeek(3, '2026-03-18').subscribe(r => (result = r));

    const req = http.expectOne(r => r.url === `${baseUrl}/week`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('trainingCenterPkid')).toBe('3');
    expect(req.request.params.get('date')).toBe('2026-03-18');
    req.flush({ weekStart: '2026-03-16', weekEnd: '2026-03-22', trainingCenterPkid: 3, items: [sample] });

    expect(result?.weekStart).toBe('2026-03-16');
    expect(result?.items.length).toBe(1);
  });

  it('getById GETs /{id}', () => {
    let result: FeaturedPromoItem | undefined;
    service.getById(7).subscribe(r => (result = r));

    const req = http.expectOne(`${baseUrl}/7`);
    expect(req.request.method).toBe('GET');
    req.flush({ ...sample, pkid: 7 });

    expect(result?.pkid).toBe(7);
  });

  it('create POSTs the request body and returns the created row', () => {
    let result: FeaturedPromoItem | undefined;
    service.create(request).subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...sample, pkid: 9 }, { status: 201, statusText: 'Created' });

    expect(result?.pkid).toBe(9);
  });

  it('update PUTs to the collection root with pkid in the body', () => {
    service.update({ ...request, pkid: 1 }).subscribe();

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(1);
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('delete DELETEs /{id}', () => {
    service.delete(3).subscribe();

    const req = http.expectOne(`${baseUrl}/3`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('moveUp POSTs /{id}/move-up with no body', () => {
    service.moveUp(4).subscribe();

    const req = http.expectOne(`${baseUrl}/4/move-up`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('moveDown POSTs /{id}/move-down with no body', () => {
    service.moveDown(4).subscribe();

    const req = http.expectOne(`${baseUrl}/4/move-down`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });
});
