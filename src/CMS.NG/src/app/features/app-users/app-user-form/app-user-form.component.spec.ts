import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AppUser } from '@core/models/app-user.model';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { AppUserFormComponent } from './app-user-form.component';

type FormInternals = {
  form: AppUserFormComponent['form'];
  isEdit: boolean;
  save: () => void;
};

describe('AppUserFormComponent', () => {
  let service: jasmine.SpyObj<AppUserService>;
  let lookup: jasmine.SpyObj<LookupService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const existing: AppUser = {
    pkid: 1,
    userId: 'helen',
    userName: 'Helen Chen',
    isActive: false,
    passwordUpdatedTime: '2026-09-01T08:00:00',
    roleCount: 1,
    roleIds: ['Admin']
  };

  async function setup(id: string | null): Promise<ComponentFixture<AppUserFormComponent>> {
    await TestBed.configureTestingModule({
      imports: [AppUserFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: AppUserService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id === null ? {} : { id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AppUserFormComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<AppUserService>('AppUserService', ['getById', 'create', 'update']);
    service.getById.and.returnValue(of(existing));
    service.create.and.returnValue(of({ ...existing, pkid: 5, userId: 'newuser' }));
    service.update.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['appRoles']);
    lookup.appRoles.and.returnValue(of([{ id: 'Admin', label: 'Administrator' }, { id: 'Editor', label: 'Editor' }]));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRow']);
    rowAudits.getForRow.and.returnValue(of([]));
  });

  describe('new mode', () => {
    it('has no password control, defaults isActive to true, and is invalid until userId and userName are set', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      expect(c.isEdit).toBeFalse();
      expect(Object.keys(c.form.controls)).toEqual(['userId', 'userName', 'isActive', 'roleIds']);
      expect(c.form.controls.isActive.value).toBeTrue();
      expect(c.form.invalid).toBeTrue();

      c.form.controls.userId.setValue('newuser');
      expect(c.form.invalid).toBeTrue();

      c.form.controls.userName.setValue('New User');
      expect(c.form.valid).toBeTrue();
    });

    it('shows the default-password note and no password input', async () => {
      const fixture = await setup(null);
      const host = fixture.nativeElement as HTMLElement;

      expect(host.textContent).toContain('系統預設密碼');
      expect(host.querySelector('input[type="password"]')).toBeNull();
    });

    it('calls create with trimmed values and roleIds, then navigates to the detail page', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      c.form.controls.userId.setValue('  newuser ');
      c.form.controls.userName.setValue(' New User ');
      c.form.controls.isActive.setValue(false);
      c.form.controls.roleIds.setValue(['Editor']);
      c.save();

      expect(service.create).toHaveBeenCalledWith({ userId: 'newuser', userName: 'New User', isActive: false, roleIds: ['Editor'] });
      expect(service.update).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/admin/app-users', 'newuser']);
    });

    it('marks userId as duplicate on 409', async () => {
      service.create.and.returnValue(throwError(() => ({ status: 409, error: { message: 'dup' } })));
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.userId.setValue('helen');
      c.form.controls.userName.setValue('Helen');
      c.save();

      expect(c.form.controls.userId.hasError('duplicate')).toBeTrue();
    });

    it('does not call the service when saving an invalid form', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(c.form.controls.userId.touched).toBeTrue();
    });
  });

  describe('edit mode', () => {
    it('loads the user, disables userId, and submits the update with the original userId', async () => {
      const fixture = await setup('helen');
      const c = fixture.componentInstance as unknown as FormInternals;
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      expect(c.isEdit).toBeTrue();
      expect(service.getById).toHaveBeenCalledWith('helen');
      expect(c.form.controls.userId.disabled).toBeTrue();
      expect(c.form.controls.userId.value).toBe('helen');
      expect(c.form.controls.isActive.value).toBeFalse();
      expect(c.form.controls.roleIds.value).toEqual(['Admin']);

      c.form.controls.userName.setValue('Helen C.');
      c.form.controls.isActive.setValue(true);
      c.form.controls.roleIds.setValue(['Admin', 'Editor']);
      c.save();

      expect(service.update).toHaveBeenCalledWith({ userId: 'helen', userName: 'Helen C.', isActive: true, roleIds: ['Admin', 'Editor'] });
      expect(service.create).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/admin/app-users', 'helen']);
    });

    it('renders the edit title and the row-audit badge, without the default-password note', async () => {
      const fixture = await setup('helen');
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

      expect(text).toContain('編輯使用者');
      expect(text).not.toContain('新使用者的密碼');
      expect(rowAudits.getForRow).toHaveBeenCalledWith('AppUser', 'helen', 1);
    });
  });
});
