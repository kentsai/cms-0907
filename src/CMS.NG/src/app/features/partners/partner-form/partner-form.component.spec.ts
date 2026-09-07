import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { Partner } from '@core/models/partner.model';
import { PartnerService } from '@core/services/partner.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { PartnerFormComponent } from './partner-form.component';

type FormInternals = {
  form: PartnerFormComponent['form'];
  isEdit: boolean;
  save: () => void;
};

describe('PartnerFormComponent', () => {
  let service: jasmine.SpyObj<PartnerService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const existing: Partner = {
    pkid: 1,
    name: 'Microsoft',
    appKey: 'MS',
    nameOnPartnerMenu: 'Microsoft 微軟',
    nameOnCourseDetailPage: '微軟',
    displayOrder: 10,
    imageFilename: null
  };

  async function setup(id: string | null): Promise<ComponentFixture<PartnerFormComponent>> {
    await TestBed.configureTestingModule({
      imports: [PartnerFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: PartnerService, useValue: service },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id === null ? {} : { id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PartnerFormComponent);
    fixture.detectChanges();
    return fixture;
  }

  function fillRequired(c: FormInternals): void {
    c.form.controls.name.setValue('Cisco');
    c.form.controls.appKey.setValue('CISCO');
    c.form.controls.nameOnPartnerMenu.setValue('Cisco 思科');
    c.form.controls.nameOnCourseDetailPage.setValue('思科');
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['getById', 'create', 'update']);
    service.getById.and.returnValue(of(existing));
    service.create.and.returnValue(of({ ...existing, pkid: 5, name: 'Cisco' }));
    service.update.and.returnValue(of(void 0));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRow']);
    rowAudits.getForRow.and.returnValue(of([]));
  });

  describe('new mode', () => {
    it('is invalid until the four required names are provided', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      expect(c.isEdit).toBeFalse();
      expect(c.form.invalid).toBeTrue();

      c.form.controls.name.setValue('Cisco');
      c.form.controls.appKey.setValue('CISCO');
      c.form.controls.nameOnPartnerMenu.setValue('Cisco 思科');
      expect(c.form.invalid).toBeTrue();

      c.form.controls.nameOnCourseDetailPage.setValue('思科');
      expect(c.form.valid).toBeTrue();
    });

    it('rejects non-ASCII or whitespace in appKey and imageFilename, and over-long names', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.appKey.setValue('微軟');
      expect(c.form.controls.appKey.hasError('pattern')).toBeTrue();
      c.form.controls.appKey.setValue('M S');
      expect(c.form.controls.appKey.hasError('pattern')).toBeTrue();
      c.form.controls.appKey.setValue('ABCDEFGHIJK');
      expect(c.form.controls.appKey.hasError('maxlength')).toBeTrue();

      c.form.controls.imageFilename.setValue('圖.png');
      expect(c.form.controls.imageFilename.hasError('pattern')).toBeTrue();

      c.form.controls.name.setValue('名'.repeat(51));
      expect(c.form.controls.name.hasError('maxlength')).toBeTrue();

      c.form.controls.nameOnPartnerMenu.setValue('名'.repeat(201));
      expect(c.form.controls.nameOnPartnerMenu.hasError('maxlength')).toBeTrue();
    });

    it('does not call the service when saving an invalid form', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;

      c.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(c.form.controls.name.touched).toBeTrue();
    });

    it('calls create with pkid 0 and a null image, then navigates to the new detail page', async () => {
      const fixture = await setup(null);
      const c = fixture.componentInstance as unknown as FormInternals;
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      fillRequired(c);
      c.form.controls.name.setValue('  Cisco  ');
      c.form.controls.displayOrder.setValue(20);
      c.form.controls.imageFilename.setValue('   ');
      c.save();

      expect(service.create).toHaveBeenCalledWith({
        pkid: 0,
        name: 'Cisco',
        appKey: 'CISCO',
        nameOnPartnerMenu: 'Cisco 思科',
        nameOnCourseDetailPage: '思科',
        displayOrder: 20,
        imageFilename: null
      });
      expect(service.update).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/course/partners', 5]);
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
      expect(c.form.controls.name.value).toBe('Microsoft');
      expect(c.form.controls.imageFilename.value).toBe('');

      c.form.controls.name.setValue('Microsoft Taiwan');
      c.form.controls.imageFilename.setValue('ms.png');
      c.save();

      expect(service.update).toHaveBeenCalledWith({
        pkid: 1,
        name: 'Microsoft Taiwan',
        appKey: 'MS',
        nameOnPartnerMenu: 'Microsoft 微軟',
        nameOnCourseDetailPage: '微軟',
        displayOrder: 10,
        imageFilename: 'ms.png'
      });
      expect(service.create).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/course/partners', 1]);
    });

    it('renders the edit title, the pkid, and the row-audit badge', async () => {
      const fixture = await setup('1');
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

      expect(text).toContain('編輯合作夥伴');
      expect(text).toContain('主代碼 1');
      expect(rowAudits.getForRow).toHaveBeenCalledWith('Partner', 1, 1);
    });
  });
});
