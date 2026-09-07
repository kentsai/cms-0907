import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AppUser } from '@core/models/app-user.model';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { AppUserDetailComponent } from './app-user-detail.component';

describe('AppUserDetailComponent', () => {
  let service: jasmine.SpyObj<AppUserService>;
  let lookup: jasmine.SpyObj<LookupService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const item: AppUser = {
    pkid: 1,
    userId: 'helen',
    userName: 'Helen Chen',
    isActive: true,
    passwordUpdatedTime: '2026-09-01T08:00:00',
    roleCount: 2,
    roleIds: ['Admin', 'Ghost']
  };

  async function setup(id = 'helen'): Promise<ComponentFixture<AppUserDetailComponent>> {
    await TestBed.configureTestingModule({
      imports: [AppUserDetailComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: AppUserService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AppUserDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<AppUserService>('AppUserService', ['getById', 'delete', 'resetPassword']);
    service.getById.and.returnValue(of(item));
    service.delete.and.returnValue(of(void 0));
    service.resetPassword.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['appRoles']);
    lookup.appRoles.and.returnValue(of([{ id: 'Admin', label: 'Administrator' }]));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRow']);
    rowAudits.getForRow.and.returnValue(of([
      { pkid: 9, tableName: 'AppUser', userName: 'system', primaryKeyValues: 'helen', actionType: 'UPDATE', actionDesc: 'UserName', dateTime: '2026-09-07T01:02:03' }
    ]));
  });

  it('loads the user by the :id route param and the role lookup', async () => {
    await setup('helen');
    expect(service.getById).toHaveBeenCalledWith('helen');
    expect(lookup.appRoles).toHaveBeenCalled();
  });

  it('renders the fields, the role tags with lookup labels, and no password value', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('helen');
    expect(text).toContain('Helen Chen');
    expect(text).toContain('啟用');
    expect(text).toContain('2026-09-01');
    expect(text).toContain('Administrator');
    expect(text).toContain('Ghost');
    expect(text).toContain('查看角色');
    expect(text).not.toContain('PasswordHash');
  });

  it('shows the row-audit badge for the record', async () => {
    const fixture = await setup();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rowAudits.getForRow).toHaveBeenCalledWith('AppUser', 'helen', 1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('最後異動 system');
  });

  it('shows a not-found message on 404', async () => {
    service.getById.and.returnValue(throwError(() => ({ status: 404 })));
    const fixture = await setup('nobody');

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('找不到使用者代碼「nobody」');
  });

  it('confirms, calls resetPassword and reloads the record', async () => {
    const fixture = await setup();
    service.getById.and.returnValue(of({ ...item, passwordUpdatedTime: '2026-09-07T10:00:00' }));
    const confirmation = TestBed.inject(ConfirmationService);
    spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.header).toBe('重設密碼');
      expect(options.message).toContain('Helen Chen');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmResetPassword: () => void }).confirmResetPassword();
    fixture.detectChanges();

    expect(service.resetPassword).toHaveBeenCalledWith('helen');
    expect(service.getById).toHaveBeenCalledTimes(2);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('2026-09-07');
  });

  it('asks for confirmation with pkid and name before deleting', async () => {
    const fixture = await setup();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const confirmation = TestBed.inject(ConfirmationService);
    spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>1</b>');
      expect(options.message).toContain('「Helen Chen」');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: () => void }).confirmDelete();

    expect(service.delete).toHaveBeenCalledWith('helen');
    expect(navigate).toHaveBeenCalledWith(['/admin/app-users']);
  });
});
