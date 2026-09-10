import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { Course, CourseRequest } from '@core/models/course.model';
import { CourseService } from './course.service';

describe('CourseService', () => {
  const baseUrl = `${environment.apiBaseUrl}/courses`;
  let service: CourseService;
  let http: HttpTestingController;

  const sample: Course = {
    pkid: 1,
    title: 'Azure 管理員',
    officialTitle: null,
    courseId: 'AZ-104',
    prodCourseId: 'AZ104',
    friendlyUrl: 'azure-administrator',
    displayOrder: 10,
    partnerPkid: 1,
    courseGroupPkid: null,
    publishStatusPkid: 1,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
    hour: 24,
    listPrice: 30000,
    learningCredit: 3.5,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    partnerName: 'Microsoft',
    courseGroupDescription: null,
    publishStatusDescription: '已發布',
    certificationPkids: [],
    jobCategoryPkids: []
  };

  const request: CourseRequest = {
    pkid: 0,
    title: sample.title,
    officialTitle: null,
    courseId: sample.courseId,
    prodCourseId: sample.prodCourseId,
    friendlyUrl: sample.friendlyUrl,
    displayOrder: 10,
    partnerPkid: 1,
    courseGroupPkid: null,
    publishStatusPkid: 1,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
    hour: 24,
    listPrice: 30000,
    learningCredit: 3.5,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    certificationPkids: [5],
    jobCategoryPkids: [1]
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CourseService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getAll GETs the collection', () => {
    let result: Course[] | undefined;
    service.getAll().subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);

    expect(result).toEqual([sample]);
  });

  it('query POSTs the filter to /query', () => {
    const query = { keyword: 'AZ', partnerPkid: 1, scheduleOnFrom: '2026-01-01', canRepeat: true };
    service.query(query).subscribe();

    const req = http.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([sample]);
  });

  it('getById GETs /{id}', () => {
    let result: Course | undefined;
    service.getById(7).subscribe(r => (result = r));

    const req = http.expectOne(`${baseUrl}/7`);
    expect(req.request.method).toBe('GET');
    req.flush({ ...sample, pkid: 7 });

    expect(result?.pkid).toBe(7);
  });

  it('create POSTs the request body with both id lists and returns the created row', () => {
    let result: Course | undefined;
    service.create(request).subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.pkid).toBe(0);
    expect(req.request.body.certificationPkids).toEqual([5]);
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
