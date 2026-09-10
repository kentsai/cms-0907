import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AppRole } from '@core/models/app-role.model';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { AppRoleFormComponent } from './app-role-form.component';

type FormInternals = {
  form: AppRoleFormComponent['form'];
  isEdit: boolean;
  save: () => void;
};

describe('AppRoleFormComponent', () => {
  let service: jasmine.SpyObj<AppRoleService>;
  let lookup: jasmine.SpyObj<LookupService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const existing: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 2,
    userIds: ['helen', 'miles']
  };

  async function setup(id: string | null): Promise<ComponentFixture<AppRoleFormComponent>> {
    await TestBed.configureTestingModule({
      imports: [AppRoleFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: AppRoleService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id === null ? {} : { id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AppRoleFormComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['getById', 'create', 'update']);
    service.getById.and.returnValue(of(existing));
    service.create.and.returnValue(of({ ...existing, roleId: 'Editor' }));
    service.update.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['appUsers']);
    lookup.appUsers.and.returnValue(of([
      { id: 'helen', label: 'helen (helen)' },
      { id: 'miles', label: 'Miles Sun (miles@uuu.com.tw)' }
    ]));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRecord']);
    rowAudits.getForRecord.and.returnValue(of([]));
  });

  describe('new mode', () => {
    it('defaults permissionLevel to 100 and is invalid until roleId and roleName are set', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      expect(c.isEdit).toBeFalse();
      expect(c.form.controls.permissionLevel.value).toBe(100);
      expect(c.form.invalid).toBeTrue();

      c.form.controls.roleId.setValue('Editor');
      expect(c.form.invalid).toBeTrue();

      c.form.controls.roleName.setValue('Editor');
      expect(c.form.valid).toBeTrue();
      expect(lookup.appUsers).toHaveBeenCalledTimes(1);
      expect(service.getById).not.toHaveBeenCalled();
    });

    it('enforces column lengths', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.roleId.setValue('r'.repeat(201));
      c.form.controls.roleName.setValue('n'.repeat(201));
      c.form.controls.description.setValue('d'.repeat(401));

      expect(c.form.controls.roleId.hasError('maxlength')).toBeTrue();
      expect(c.form.controls.roleName.hasError('maxlength')).toBeTrue();
      expect(c.form.controls.description.hasError('maxlength')).toBeTrue();
    });

    it('does not call the service when saving an invalid form', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(c.form.controls.roleId.touched).toBeTrue();
    });

    it('calls create with trimmed values, null description and userIds, then navigates to detail', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      c.form.controls.roleId.setValue('  Editor ');
      c.form.controls.roleName.setValue('Editor');
      c.form.controls.description.setValue('   ');
      c.form.controls.userIds.setValue(['helen']);
      c.save();

      expect(service.create).toHaveBeenCalledWith({
        roleId: 'Editor',
        roleName: 'Editor',
        permissionLevel: 100,
        description: null,
        userIds: ['helen']
      });
      expect(service.update).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/admin/app-roles', 'Editor']);
    });

    it('marks roleId as duplicate when the API returns 409', async () => {
      service.create.and.returnValue(throwError(() => ({ status: 409, error: { message: '角色代碼「Admin」已存在。' } })));
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.roleId.setValue('Admin');
      c.form.controls.roleName.setValue('Administrator');
      c.save();

      expect(c.form.controls.roleId.hasError('duplicate')).toBeTrue();
    });
  });

  describe('edit mode', () => {
    it('loads the role, disables roleId, and submits it via getRawValue on update', async () => {
      const fixture = await setup('Admin');
      const c = fixture.componentInstance as unknown as FormInternals;
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      expect(c.isEdit).toBeTrue();
      expect(service.getById).toHaveBeenCalledWith('Admin');
      expect(c.form.controls.roleId.disabled).toBeTrue();
      expect(c.form.controls.roleName.value).toBe('Administrator');
      expect(c.form.controls.userIds.value).toEqual(['helen', 'miles']);

      c.form.controls.roleName.setValue('Administrators');
      c.form.controls.userIds.setValue(['helen']);
      c.save();

      expect(service.update).toHaveBeenCalledWith({
        roleId: 'Admin',
        roleName: 'Administrators',
        permissionLevel: 1,
        description: '系統管理員',
        userIds: ['helen']
      });
      expect(service.create).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/admin/app-roles', 'Admin']);
    });

    it('renders the edit title and requests the row-audit badge by RoleId', async () => {
      const fixture = await setup('Admin');
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

      expect(text).toContain('編輯角色');
      expect(rowAudits.getForRecord).toHaveBeenCalledWith('AppRole', 'Admin');
    });
  });
});
