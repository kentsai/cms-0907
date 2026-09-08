import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { CourseGroup } from '@core/models/course-group.model';
import { CourseGroupService } from '@core/services/course-group.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { CourseGroupFormComponent } from './course-group-form.component';

type FormInternals = {
  form: CourseGroupFormComponent['form'];
  isEdit: boolean;
  save: () => void;
};

describe('CourseGroupFormComponent', () => {
  let service: jasmine.SpyObj<CourseGroupService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const existing: CourseGroup = { pkid: 1, description: '雲端運算' };

  async function setup(id: string | null): Promise<ComponentFixture<CourseGroupFormComponent>> {
    await TestBed.configureTestingModule({
      imports: [CourseGroupFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: CourseGroupService, useValue: service },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id === null ? {} : { id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CourseGroupFormComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['getById', 'create', 'update']);
    service.getById.and.returnValue(of(existing));
    service.create.and.returnValue(of({ pkid: 5, description: '資訊安全' }));
    service.update.and.returnValue(of(void 0));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRecord']);
    rowAudits.getForRecord.and.returnValue(of([]));
  });

  describe('new mode', () => {
    it('is invalid until the description is provided', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      expect(c.isEdit).toBeFalse();
      expect(c.form.invalid).toBeTrue();

      c.form.controls.description.setValue('資訊安全');
      expect(c.form.valid).toBeTrue();
    });

    it('rejects an over-long description', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.description.setValue('名'.repeat(101));
      expect(c.form.controls.description.hasError('maxlength')).toBeTrue();

      c.form.controls.description.setValue('名'.repeat(100));
      expect(c.form.valid).toBeTrue();
    });

    it('does not call the service when saving an invalid form', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(c.form.controls.description.touched).toBeTrue();
    });

    it('calls create with pkid 0 and a trimmed description, then navigates to the new detail page', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      c.form.controls.description.setValue('  資訊安全  ');
      c.save();

      expect(service.create).toHaveBeenCalledWith({ pkid: 0, description: '資訊安全' });
      expect(service.update).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/course/course-groups', 5]);
    });
  });

  describe('edit mode', () => {
    it('loads the record and submits the update with the original pkid', async () => {
      const fixture = await setup('1');
      const c = fixture.componentInstance as unknown as FormInternals;
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      expect(c.isEdit).toBeTrue();
      expect(service.getById).toHaveBeenCalledWith(1);
      expect(c.form.controls.description.value).toBe('雲端運算');

      c.form.controls.description.setValue('雲端與 DevOps');
      c.save();

      expect(service.update).toHaveBeenCalledWith({ pkid: 1, description: '雲端與 DevOps' });
      expect(service.create).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/course/course-groups', 1]);
    });

    it('renders the edit title, the pkid, and the row-audit badge', async () => {
      const fixture = await setup('1');
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

      expect(text).toContain('編輯課程群組');
      expect(text).toContain('主代碼 1');
      expect(rowAudits.getForRecord).toHaveBeenCalledWith('CourseGroup', 1);
    });
  });
});
