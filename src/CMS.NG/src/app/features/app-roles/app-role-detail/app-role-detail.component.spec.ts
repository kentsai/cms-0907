import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AppRole } from '@core/models/app-role.model';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { AppRoleDetailComponent } from './app-role-detail.component';

describe('AppRoleDetailComponent', () => {
  let service: jasmine.SpyObj<AppRoleService>;
  let lookup: jasmine.SpyObj<LookupService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const item: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 2,
    userIds: ['helen', 'ghost']
  };

  async function setup(id = 'Admin'): Promise<ComponentFixture<AppRoleDetailComponent>> {
    await TestBed.configureTestingModule({
      imports: [AppRoleDetailComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: AppRoleService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AppRoleDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['getById', 'delete']);
    service.getById.and.returnValue(of(item));
    service.delete.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['appUsers']);
    lookup.appUsers.and.returnValue(of([{ id: 'helen', label: 'helen (helen)' }]));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRecord']);
    rowAudits.getForRecord.and.returnValue(of([]));
  });

  it('loads the role by the string :id route param', async () => {
    await setup('Admin');
    expect(service.getById).toHaveBeenCalledWith('Admin');
    expect(rowAudits.getForRecord).toHaveBeenCalledWith('AppRole', 'Admin');
  });

  it('renders the role fields', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('主代碼');
    expect(text).toContain('Admin');
    expect(text).toContain('Administrator');
    expect(text).toContain('系統管理員');
    expect(text).toContain('權限等級');
  });

  it('renders assigned users with lookup labels, falling back to the raw id', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('helen (helen)');
    expect(text).toContain('ghost');
    expect(text).toContain('2 位');
  });

  it('shows a not-found message on 404', async () => {
    service.getById.and.returnValue(throwError(() => ({ status: 404 })));
    const fixture = await setup('nope');

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('找不到角色代碼「nope」');
  });
});
