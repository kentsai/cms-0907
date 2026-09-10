import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { AppUser, AppUserRequest } from '@core/models/app-user.model';
import { AppUserService } from './app-user.service';

describe('AppUserService', () => {
  const baseUrl = `${environment.apiBaseUrl}/app-users`;
  let service: AppUserService;
  let http: HttpTestingController;

  const sample: AppUser = {
    pkid: 1,
    userId: 'helen',
    userName: 'Helen Chen',
    isActive: true,
    passwordUpdatedTime: '2026-09-01T08:00:00',
    roleCount: 2,
    roleIds: ['Admin', 'Editor']
  };

  const request: AppUserRequest = { userId: 'helen', userName: 'Helen Chen', isActive: true, roleIds: ['Admin'] };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AppUserService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getAll GETs the collection', () => {
    let result: AppUser[] | undefined;
    service.getAll().subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);

    expect(result).toEqual([sample]);
  });

  it('query POSTs the filter to /query', () => {
    const query = { keyword: 'hel', isActive: true, roleId: 'Admin' };
    service.query(query).subscribe();

    const req = http.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([sample]);
  });

  it('getById URL-encodes the string key', () => {
    service.getById('miles@uuu.com.tw/x y').subscribe();

    const req = http.expectOne(`${baseUrl}/miles%40uuu.com.tw%2Fx%20y`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('create POSTs the request body without any password member', () => {
    let result: AppUser | undefined;
    service.create(request).subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(Object.keys(req.request.body)).toEqual(['userId', 'userName', 'isActive', 'roleIds']);
    req.flush({ ...sample, pkid: 9 }, { status: 201, statusText: 'Created' });

    expect(result?.pkid).toBe(9);
  });

  it('update PUTs to the collection root with userId in the body', () => {
    service.update(request).subscribe();

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.userId).toBe('helen');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('delete URL-encodes the string key', () => {
    service.delete('a/b c').subscribe();

    const req = http.expectOne(`${baseUrl}/a%2Fb%20c`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('resetPassword POSTs to /{id}/reset-password with an empty body', () => {
    service.resetPassword('a/b').subscribe();

    const req = http.expectOne(`${baseUrl}/a%2Fb/reset-password`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    req.flush(null, { status: 204, statusText: 'No Content' });
  });
});
