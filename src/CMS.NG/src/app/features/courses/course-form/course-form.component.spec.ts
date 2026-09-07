import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { Course } from '@core/models/course.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { CourseFormComponent } from './course-form.component';

type FormInternals = {
  form: CourseFormComponent['form'];
  isEdit: boolean;
  save: () => void;
};

describe('CourseFormComponent', () => {
  let service: jasmine.SpyObj<CourseService>;
  let lookup: jasmine.SpyObj<LookupService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const existing: Course = {
    pkid: 1,
    title: 'Azure 管理員',
    officialTitle: 'Microsoft Azure Administrator',
    courseId: 'AZ-104',
    prodCourseId: 'AZ104',
    friendlyUrl: 'azure-administrator',
    displayOrder: 10,
    partnerPkid: 1,
    courseGroupPkid: 2,
    publishStatusPkid: 1,
    scheduleOn: '2026-01-01',
    scheduleOff: '2031-06-30',
    hour: 24,
    listPrice: 30000,
    learningCredit: 3.5,
    material: '原廠教材',
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    partnerName: 'Microsoft',
    courseGroupDescription: '雲端',
    publishStatusDescription: '已發布',
    certificationPkids: [5],
    jobCategoryPkids: [1, 2]
  };

  async function setup(id: string | null): Promise<ComponentFixture<CourseFormComponent>> {
    await TestBed.configureTestingModule({
      imports: [CourseFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id === null ? {} : { id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CourseFormComponent);
    fixture.detectChanges();
    return fixture;
  }

  function fillRequired(c: FormInternals): void {
    c.form.controls.title.setValue('CCNA 認證班');
    c.form.controls.courseId.setValue('CCNA');
    c.form.controls.prodCourseId.setValue('CCNA1');
    c.form.controls.friendlyUrl.setValue('ccna');
    c.form.controls.partnerPkid.setValue(2);
    c.form.controls.publishStatusPkid.setValue(1);
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['getById', 'create', 'update']);
    service.getById.and.returnValue(of(existing));
    service.create.and.returnValue(of({ ...existing, pkid: 5, courseId: 'CCNA' }));
    service.update.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['partners', 'courseGroups', 'publishStatuses', 'certifications', 'jobCategories']);
    lookup.partners.and.returnValue(of([{ pkid: 1, label: 'Microsoft' }, { pkid: 2, label: 'Cisco' }]));
    lookup.courseGroups.and.returnValue(of([{ pkid: 2, label: '雲端' }]));
    lookup.publishStatuses.and.returnValue(of([{ pkid: 1, label: '已發布' }]));
    lookup.certifications.and.returnValue(of([{ pkid: 5, label: 'Microsoft Azure Administrator Associate' }]));
    lookup.jobCategories.and.returnValue(of([{ pkid: 1, label: '系統管理' }, { pkid: 2, label: '網路' }]));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRow']);
    rowAudits.getForRow.and.returnValue(of([]));
  });

  describe('new mode', () => {
    it('loads all five lookups and pre-fills today / today+10y for the schedule dates', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      expect(c.isEdit).toBeFalse();
      expect(lookup.partners).toHaveBeenCalled();
      expect(lookup.courseGroups).toHaveBeenCalled();
      expect(lookup.publishStatuses).toHaveBeenCalled();
      expect(lookup.certifications).toHaveBeenCalled();
      expect(lookup.jobCategories).toHaveBeenCalled();
      expect(service.getById).not.toHaveBeenCalled();

      const on = c.form.controls.scheduleOn.value!;
      const off = c.form.controls.scheduleOff.value!;
      const today = new Date();
      expect(on.getFullYear()).toBe(today.getFullYear());
      expect(on.getDate()).toBe(today.getDate());
      expect(off.getFullYear()).toBe(today.getFullYear() + 10);
    });

    it('is invalid until the required fields are provided', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      expect(c.form.invalid).toBeTrue();

      c.form.controls.title.setValue('CCNA 認證班');
      c.form.controls.courseId.setValue('CCNA');
      c.form.controls.prodCourseId.setValue('CCNA1');
      c.form.controls.friendlyUrl.setValue('ccna');
      c.form.controls.partnerPkid.setValue(2);
      expect(c.form.invalid).toBeTrue();

      c.form.controls.publishStatusPkid.setValue(1);
      expect(c.form.valid).toBeTrue();
    });

    it('rejects non-ASCII codes, over-long text and negative numbers', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.courseId.setValue('課程 代碼');
      expect(c.form.controls.courseId.hasError('pattern')).toBeTrue();
      c.form.controls.prodCourseId.setValue('A B');
      expect(c.form.controls.prodCourseId.hasError('pattern')).toBeTrue();

      c.form.controls.title.setValue('名'.repeat(201));
      expect(c.form.controls.title.hasError('maxlength')).toBeTrue();
      c.form.controls.material.setValue('名'.repeat(501));
      expect(c.form.controls.material.hasError('maxlength')).toBeTrue();

      c.form.controls.listPrice.setValue(-1);
      expect(c.form.controls.listPrice.hasError('min')).toBeTrue();
      c.form.controls.hour.setValue(null);
      expect(c.form.controls.hour.hasError('required')).toBeTrue();
    });

    it('re-defaults scheduleOff to scheduleOn + 10 years when scheduleOn changes', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.scheduleOn.setValue(new Date(2027, 2, 15));

      const off = c.form.controls.scheduleOff.value!;
      expect(off.getFullYear()).toBe(2037);
      expect(off.getMonth()).toBe(2);
      expect(off.getDate()).toBe(15);
    });

    it('flags scheduleOff earlier than scheduleOn at group level', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;
      fillRequired(c);

      c.form.controls.scheduleOn.setValue(new Date(2027, 0, 1));
      c.form.controls.scheduleOff.setValue(new Date(2026, 11, 31));
      expect(c.form.hasError('scheduleRange')).toBeTrue();
      expect(c.form.invalid).toBeTrue();

      c.form.controls.scheduleOff.setValue(new Date(2027, 0, 1));
      expect(c.form.hasError('scheduleRange')).toBeFalse();
      expect(c.form.valid).toBeTrue();
    });

    it('does not call the service when saving an invalid form', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(c.form.controls.title.touched).toBeTrue();
    });

    it('calls create with pkid 0, ISO dates, trimmed text, nulls for blanks and both id lists', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      fillRequired(c);
      c.form.controls.title.setValue('  CCNA 認證班  ');
      c.form.controls.officialTitle.setValue('   ');
      c.form.controls.scheduleOn.setValue(new Date(2027, 0, 5));
      c.form.controls.displayOrder.setValue(20);
      c.form.controls.hour.setValue(40);
      c.form.controls.listPrice.setValue(45000);
      c.form.controls.learningCredit.setValue(4.5);
      c.form.controls.canRepeat.setValue(true);
      c.form.controls.material.setValue(' 教材 ');
      c.form.controls.certificationPkids.setValue([5]);
      c.form.controls.jobCategoryPkids.setValue([2]);
      c.save();

      expect(service.create).toHaveBeenCalledWith(jasmine.objectContaining({
        pkid: 0,
        title: 'CCNA 認證班',
        officialTitle: null,
        courseId: 'CCNA',
        prodCourseId: 'CCNA1',
        friendlyUrl: 'ccna',
        displayOrder: 20,
        partnerPkid: 2,
        courseGroupPkid: null,
        publishStatusPkid: 1,
        scheduleOn: '2027-01-05',
        scheduleOff: '2037-01-05',
        hour: 40,
        listPrice: 45000,
        learningCredit: 4.5,
        material: '教材',
        objective: null,
        canRepeat: true,
        certificationPkids: [5],
        jobCategoryPkids: [2]
      }));
      expect(service.update).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/course/courses', 5]);
    });
  });

  describe('edit mode', () => {
    it('loads the record, keeps the stored scheduleOff, and submits the update with the original pkid', async () => {
      const fixture = await setup('1');
      const c = fixture.componentInstance as unknown as FormInternals;
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      expect(c.isEdit).toBeTrue();
      expect(service.getById).toHaveBeenCalledWith(1);
      expect(c.form.controls.title.value).toBe('Azure 管理員');
      expect(c.form.controls.partnerPkid.value).toBe(1);
      expect(c.form.controls.courseGroupPkid.value).toBe(2);
      expect(c.form.controls.scheduleOn.value?.getFullYear()).toBe(2026);
      // The +10y default must NOT overwrite the stored 2031 value.
      expect(c.form.controls.scheduleOff.value?.getFullYear()).toBe(2031);
      expect(c.form.controls.certificationPkids.value).toEqual([5]);
      expect(c.form.controls.jobCategoryPkids.value).toEqual([1, 2]);

      c.form.controls.title.setValue('Azure 管理員（新版）');
      c.form.controls.courseGroupPkid.setValue(null);
      c.form.controls.jobCategoryPkids.setValue([1]);
      c.save();

      expect(service.update).toHaveBeenCalledWith(jasmine.objectContaining({
        pkid: 1,
        title: 'Azure 管理員（新版）',
        courseId: 'AZ-104',
        courseGroupPkid: null,
        scheduleOn: '2026-01-01',
        scheduleOff: '2031-06-30',
        material: '原廠教材',
        certificationPkids: [5],
        jobCategoryPkids: [1]
      }));
      expect(service.create).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/course/courses', 1]);
    });

    it('renders the edit title, the pkid, and the row-audit badge', async () => {
      const fixture = await setup('1');
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

      expect(text).toContain('編輯課程');
      expect(text).toContain('主代碼 1');
      expect(rowAudits.getForRow).toHaveBeenCalledWith('Course', 1, 1);
    });
  });
});
