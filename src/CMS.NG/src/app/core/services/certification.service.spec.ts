import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { Certification, CertificationRequest } from '@core/models/certification.model';
import { CertificationService } from './certification.service';

describe('CertificationService', () => {
  const baseUrl = `${environment.apiBaseUrl}/certifications`;
  let service: CertificationService;
  let http: HttpTestingController;

  const sample: Certification = {
    pkid: 1,
    partnerPkid: 1,
    title: 'Azure Administrator Associate',
    partnerName: 'Microsoft',
    coursePkids: [],
    jobCategoryPkids: []
  };

  const request: CertificationRequest = {
    pkid: 0,
    partnerPkid: 1,
    title: sample.title,
    coursePkids: [10, 11],
    jobCategoryPkids: [1]
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CertificationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getAll GETs the collection', () => {
    let result: Certification[] | undefined;
    service.getAll().subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);

    expect(result).toEqual([sample]);
  });

  it('query POSTs the filter to /query', () => {
    const query = { keyword: 'Azure', partnerPkid: 1, coursePkid: 10, jobCategoryPkid: 1 };
    service.query(query).subscribe();

    const req = http.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([sample]);
  });

  it('getById GETs /{id}', () => {
    let result: Certification | undefined;
    service.getById(7).subscribe(r => (result = r));

    const req = http.expectOne(`${baseUrl}/7`);
    expect(req.request.method).toBe('GET');
    req.flush({ ...sample, pkid: 7 });

    expect(result?.pkid).toBe(7);
  });

  it('create POSTs the request body with both id lists and returns the created row', () => {
    let result: Certification | undefined;
    service.create(request).subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.pkid).toBe(0);
    expect(req.request.body.coursePkids).toEqual([10, 11]);
    expect(req.request.body.jobCategoryPkids).toEqual([1]);
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
});
