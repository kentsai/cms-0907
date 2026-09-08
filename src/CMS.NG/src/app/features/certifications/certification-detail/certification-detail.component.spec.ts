import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { Certification } from '@core/models/certification.model';
import { CertificationService } from '@core/services/certification.service';
import { LookupService } from '@core/services/lookup.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { CertificationDetailComponent } from './certification-detail.component';

describe('CertificationDetailComponent', () => {
  let service: jasmine.SpyObj<CertificationService>;
  let lookup: jasmine.SpyObj<LookupService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const item: Certification = {
    pkid: 1,
    partnerPkid: 1,
    title: 'Azure Administrator Associate',
    partnerName: 'Microsoft',
    coursePkids: [10, 99],
    jobCategoryPkids: [1]
  };

  async function setup(id = '1'): Promise<ComponentFixture<CertificationDetailComponent>> {
    await TestBed.configureTestingModule({
      imports: [CertificationDetailComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: CertificationService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CertificationDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<CertificationService>('CertificationService', ['getById', 'delete']);
    service.getById.and.returnValue(of(item));
    service.delete.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['courses', 'jobCategories']);
    lookup.courses.and.returnValue(of([{ pkid: 10, label: 'AZ-104 Azure 管理員' }]));
    lookup.jobCategories.and.returnValue(of([{ pkid: 1, label: '系統管理' }]));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRow']);
    rowAudits.getForRow.and.returnValue(of([
      { pkid: 9, tableName: 'Certification', userName: 'system', primaryKeyValues: '1', actionType: 'UPDATE', actionDesc: 'Title', dateTime: '2026-09-08T01:02:03' }
    ]));
  });

  it('loads the record and the two N-N lookups from the :id route param', async () => {
    await setup('1');
    expect(service.getById).toHaveBeenCalledWith(1);
    expect(lookup.courses).toHaveBeenCalled();
    expect(lookup.jobCategories).toHaveBeenCalled();
  });

  it('renders the fields with the partner as a link', async () => {
    const fixture = await setup();
    const host = fixture.nativeElement as HTMLElement;
    const text = host.textContent ?? '';

    expect(text).toContain('Azure Administrator Associate');
    expect(text).toContain('Microsoft');

    const links = Array.from(host.querySelectorAll('a.fk-link')).map(a => a.textContent?.trim());
    expect(links).toEqual(['Microsoft']);
  });

  it('shows a dash and the untitled placeholder when the title is null', async () => {
    service.getById.and.returnValue(of({ ...item, title: null }));
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('(無名稱)');
    expect(text).toContain('—');
  });

  it('resolves course and job-category labels, falling back to #pkid', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('AZ-104 Azure 管理員、#99');
    expect(text).toContain('系統管理');
  });

  it('shows the row-audit badge for the record', async () => {
    const fixture = await setup();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rowAudits.getForRow).toHaveBeenCalledWith('Certification', 1, 1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('最後異動 system');
  });

  it('shows a not-found message on 404', async () => {
    service.getById.and.returnValue(throwError(() => ({ status: 404 })));
    const fixture = await setup('99');

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('找不到主代碼 99');
  });

  it('asks for confirmation with pkid and title before deleting', async () => {
    const fixture = await setup();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const confirmation = TestBed.inject(ConfirmationService);
    spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>1</b>');
      expect(options.message).toContain('「Azure Administrator Associate」');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: () => void }).confirmDelete();

    expect(service.delete).toHaveBeenCalledWith(1);
    expect(navigate).toHaveBeenCalledWith(['/course/certifications']);
  });
});
