import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { PublishStatus } from '@core/models/publish-status.model';
import { PublishStatusService } from './publish-status.service';

describe('PublishStatusService', () => {
  const baseUrl = `${environment.apiBaseUrl}/publish-statuses`;
  let service: PublishStatusService;
  let http: HttpTestingController;

  const sample: PublishStatus = {
    pkid: 1,
    description: '草稿',
    isDraft: true,
    isPublished: false,
    isDiscontinued: false
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(PublishStatusService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getAll GETs the collection', () => {
    let result: PublishStatus[] | undefined;
    service.getAll().subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);

    expect(result).toEqual([sample]);
  });

  it('query POSTs the filter to /query', () => {
    const query = { keyword: '草', isDraft: true, isPublished: null, isDiscontinued: null };
    service.query(query).subscribe();

    const req = http.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([sample]);
  });

  it('getById GETs /{id}', () => {
    let result: PublishStatus | undefined;
    service.getById(7).subscribe(r => (result = r));

    const req = http.expectOne(`${baseUrl}/7`);
    expect(req.request.method).toBe('GET');
    req.flush({ ...sample, pkid: 7 });

    expect(result?.pkid).toBe(7);
  });

  it('create POSTs the request body', () => {
    service.create(sample).subscribe();

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(sample);
    req.flush(sample, { status: 201, statusText: 'Created' });
  });

  it('update PUTs to the collection root with pkid in the body', () => {
    service.update(sample).subscribe();

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
});
