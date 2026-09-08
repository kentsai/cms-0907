import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { Certification } from '@core/models/certification.model';
import { CertificationService } from '@core/services/certification.service';
import { LookupService } from '@core/services/lookup.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { CertificationFormComponent } from './certification-form.component';

type FormInternals = {
  form: CertificationFormComponent['form'];
  isEdit: boolean;
  save: () => void;
};

describe('CertificationFormComponent', () => {
  let service: jasmine.SpyObj<CertificationService>;
  let lookup: jasmine.SpyObj<LookupService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const existing: Certification = {
    pkid: 1,
    partnerPkid: 1,
    title: 'Azure Administrator Associate',
    partnerName: 'Microsoft',
    coursePkids: [10],
    jobCategoryPkids: [1, 2]
  };

  async function setup(id: string | null): Promise<ComponentFixture<CertificationFormComponent>> {
    await TestBed.configureTestingModule({
      imports: [CertificationFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: CertificationService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id === null ? {} : { id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CertificationFormComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<CertificationService>('CertificationService', ['getById', 'create', 'update']);
    service.getById.and.returnValue(of(existing));
    service.create.and.returnValue(of({ ...existing, pkid: 5, partnerPkid: 2, partnerName: 'Cisco', title: 'CCNA' }));
    service.update.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['partners', 'courses', 'jobCategories']);
    lookup.partners.and.returnValue(of([{ pkid: 1, label: 'Microsoft' }, { pkid: 2, label: 'Cisco' }]));
    lookup.courses.and.returnValue(of([{ pkid: 10, label: 'AZ-104 Azure 管理員' }, { pkid: 11, label: 'CCNA CCNA 認證班' }]));
    lookup.jobCategories.and.returnValue(of([{ pkid: 1, label: '系統管理' }, { pkid: 2, label: '網路' }]));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRow']);
    rowAudits.getForRow.and.returnValue(of([]));
  });

  describe('new mode', () => {
    it('loads the three lookups and does not fetch a record', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      expect(c.isEdit).toBeFalse();
      expect(lookup.partners).toHaveBeenCalled();
      expect(lookup.courses).toHaveBeenCalled();
      expect(lookup.jobCategories).toHaveBeenCalled();
      expect(service.getById).not.toHaveBeenCalled();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('新增認證');
    });

    it('is invalid until a partner is selected; title stays optional', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      expect(c.form.invalid).toBeTrue();

      c.form.controls.partnerPkid.setValue(2);
      expect(c.form.valid).toBeTrue();
    });

    it('rejects a title longer than 100 characters', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.title.setValue('名'.repeat(101));
      expect(c.form.controls.title.hasError('maxlength')).toBeTrue();

      c.form.controls.title.setValue('名'.repeat(100));
      expect(c.form.controls.title.valid).toBeTrue();
    });

    it('does not call the service when saving an invalid form', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(c.form.controls.partnerPkid.touched).toBeTrue();
    });

    it('calls create with pkid 0, a trimmed title and both id lists, then navigates to the new pkid', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      c.form.controls.partnerPkid.setValue(2);
      c.form.controls.title.setValue('  CCNA  ');
      c.form.controls.coursePkids.setValue([11]);
      c.form.controls.jobCategoryPkids.setValue([2]);
      c.save();

      expect(service.create).toHaveBeenCalledWith({
        pkid: 0,
        partnerPkid: 2,
        title: 'CCNA',
        coursePkids: [11],
        jobCategoryPkids: [2]
      });
      expect(service.update).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/course/certifications', 5]);
    });

    it('sends null for a blank title', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;
      spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      c.form.controls.partnerPkid.setValue(1);
      c.form.controls.title.setValue('   ');
      c.save();

      expect(service.create).toHaveBeenCalledWith(jasmine.objectContaining({ title: null, coursePkids: [], jobCategoryPkids: [] }));
    });
  });

  describe('edit mode', () => {
    it('loads the record into the form and submits the update with the original pkid', async () => {
      const fixture = await setup('1');
      const c = fixture.componentInstance as unknown as FormInternals;
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      expect(c.isEdit).toBeTrue();
      expect(service.getById).toHaveBeenCalledWith(1);
      expect(c.form.controls.partnerPkid.value).toBe(1);
      expect(c.form.controls.title.value).toBe('Azure Administrator Associate');
      expect(c.form.controls.coursePkids.value).toEqual([10]);
      expect(c.form.controls.jobCategoryPkids.value).toEqual([1, 2]);

      c.form.controls.title.setValue('Azure Administrator Associate (AZ-104)');
      c.form.controls.jobCategoryPkids.setValue([1]);
      c.save();

      expect(service.update).toHaveBeenCalledWith({
        pkid: 1,
        partnerPkid: 1,
        title: 'Azure Administrator Associate (AZ-104)',
        coursePkids: [10],
        jobCategoryPkids: [1]
      });
      expect(service.create).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/course/certifications', 1]);
    });

    it('renders the edit title, the pkid, and the row-audit badge', async () => {
      const fixture = await setup('1');
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

      expect(text).toContain('編輯認證');
      expect(text).toContain('主代碼 1');
      expect(rowAudits.getForRow).toHaveBeenCalledWith('Certification', 1, 1);
    });
  });
});
