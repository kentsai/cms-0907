import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { PublishStatus } from '@core/models/publish-status.model';
import { PublishStatusService } from '@core/services/publish-status.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { PublishStatusFormComponent } from './publish-status-form.component';

type FormInternals = {
  form: PublishStatusFormComponent['form'];
  isEdit: boolean;
  save: () => void;
};

describe('PublishStatusFormComponent', () => {
  let service: jasmine.SpyObj<PublishStatusService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const existing: PublishStatus = { pkid: 1, description: '草稿', isDraft: true, isPublished: false, isDiscontinued: false };

  async function setup(id: string | null): Promise<ComponentFixture<PublishStatusFormComponent>> {
    await TestBed.configureTestingModule({
      imports: [PublishStatusFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: PublishStatusService, useValue: service },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id === null ? {} : { id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PublishStatusFormComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['getById', 'create', 'update']);
    service.getById.and.returnValue(of(existing));
    service.create.and.returnValue(of({ ...existing, pkid: 5 }));
    service.update.and.returnValue(of(void 0));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRow']);
    rowAudits.getForRow.and.returnValue(of([]));
  });

  describe('new mode', () => {
    it('is invalid until pkid and description are provided', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      expect(c.isEdit).toBeFalse();
      expect(c.form.invalid).toBeTrue();

      c.form.controls.pkid.setValue(5);
      expect(c.form.invalid).toBeTrue();

      c.form.controls.description.setValue('已發布');
      expect(c.form.valid).toBeTrue();
    });

    it('rejects pkid outside the tinyint range and description over 50 chars', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.pkid.setValue(256);
      expect(c.form.controls.pkid.hasError('max')).toBeTrue();

      c.form.controls.pkid.setValue(-1);
      expect(c.form.controls.pkid.hasError('min')).toBeTrue();

      c.form.controls.description.setValue('狀'.repeat(51));
      expect(c.form.controls.description.hasError('maxlength')).toBeTrue();
    });

    it('does not call the service when saving an invalid form', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(c.form.controls.description.touched).toBeTrue();
    });

    it('calls create and navigates to the detail page on save', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      c.form.controls.pkid.setValue(5);
      c.form.controls.description.setValue('  已發布  ');
      c.form.controls.isPublished.setValue(true);
      c.save();

      expect(service.create).toHaveBeenCalledWith({
        pkid: 5,
        description: '已發布',
        isDraft: false,
        isPublished: true,
        isDiscontinued: false
      });
      expect(service.update).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/admin/publish-statuses', 5]);
    });

    it('marks pkid as duplicate when the API returns 409', async () => {
      service.create.and.returnValue(throwError(() => ({ status: 409, error: { message: '主代碼 1 已存在。' } })));
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.pkid.setValue(1);
      c.form.controls.description.setValue('草稿');
      c.save();

      expect(c.form.controls.pkid.hasError('duplicate')).toBeTrue();
    });
  });

  describe('edit mode', () => {
    it('loads the record, disables pkid, and still submits it via getRawValue', async () => {
      const fixture = await setup('1');
      const c = fixture.componentInstance as unknown as FormInternals;
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      expect(c.isEdit).toBeTrue();
      expect(service.getById).toHaveBeenCalledWith(1);
      expect(c.form.controls.pkid.disabled).toBeTrue();
      expect(c.form.controls.description.value).toBe('草稿');

      c.form.controls.description.setValue('草稿(修)');
      c.save();

      expect(service.update).toHaveBeenCalledWith({
        pkid: 1,
        description: '草稿(修)',
        isDraft: true,
        isPublished: false,
        isDiscontinued: false
      });
      expect(service.create).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/admin/publish-statuses', 1]);
    });

    it('renders the edit title and the row-audit badge', async () => {
      const fixture = await setup('1');
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

      expect(text).toContain('編輯發布狀態');
      expect(rowAudits.getForRow).toHaveBeenCalledWith('PublishStatus', 1, 1);
    });
  });
});
