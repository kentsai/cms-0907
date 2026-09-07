import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { CourseGroup } from '@core/models/course-group.model';
import { CourseGroupService } from './course-group.service';

describe('CourseGroupService', () => {
  const baseUrl = `${environment.apiBaseUrl}/course-groups`;
  let service: CourseGroupService;
  let http: HttpTestingController;

  const sample: CourseGroup = { pkid: 1, description: '雲端運算' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CourseGroupService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getAll GETs the collection', () => {
    let result: CourseGroup[] | undefined;
    service.getAll().subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);

    expect(result).toEqual([sample]);
  });

  it('query POSTs the filter to /query', () => {
    const query = { keyword: '雲端' };
    service.query(query).subscribe();

    const req = http.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([sample]);
  });

  it('getById GETs /{id}', () => {
    let result: CourseGroup | undefined;
    service.getById(7).subscribe(r => (result = r));

    const req = http.expectOne(`${baseUrl}/7`);
    expect(req.request.method).toBe('GET');
    req.flush({ ...sample, pkid: 7 });

    expect(result?.pkid).toBe(7);
  });

  it('create POSTs the request body and returns the created row', () => {
    let result: CourseGroup | undefined;
    service.create({ ...sample, pkid: 0 }).subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.pkid).toBe(0);
    req.flush({ ...sample, pkid: 9 }, { status: 201, statusText: 'Created' });

    expect(result?.pkid).toBe(9);
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
