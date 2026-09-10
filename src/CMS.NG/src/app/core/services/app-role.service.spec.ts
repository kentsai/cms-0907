import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { AppRole, AppRoleRequest } from '@core/models/app-role.model';
import { AppRoleService } from './app-role.service';

describe('AppRoleService', () => {
  const baseUrl = `${environment.apiBaseUrl}/app-roles`;
  let service: AppRoleService;
  let http: HttpTestingController;

  const sample: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 2,
    userIds: ['helen', 'miles']
  };

  const request: AppRoleRequest = {
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userIds: ['helen', 'miles']
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AppRoleService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getAll GETs the collection', () => {
    let result: AppRole[] | undefined;
    service.getAll().subscribe(r => (result = r));

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);

    expect(result).toEqual([sample]);
  });

  it('query POSTs the filter to /query', () => {
    const query = { keyword: 'Adm', permissionLevel: 1, userId: 'helen' };
    service.query(query).subscribe();

    const req = http.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([sample]);
  });

  it('getById URL-encodes the string RoleId', () => {
    service.getById('a/b c').subscribe();

    const req = http.expectOne(`${baseUrl}/a%2Fb%20c`);
    expect(req.request.method).toBe('GET');
    req.flush({ ...sample, roleId: 'a/b c' });
  });

  it('create POSTs the request body', () => {
    service.create(request).subscribe();

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(sample, { status: 201, statusText: 'Created' });
  });

  it('update PUTs to the collection root with roleId in the body', () => {
    service.update(request).subscribe();

    const req = http.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.roleId).toBe('Admin');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('delete URL-encodes the string RoleId', () => {
    service.delete('a/b c').subscribe();

    const req = http.expectOne(`${baseUrl}/a%2Fb%20c`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });
});
