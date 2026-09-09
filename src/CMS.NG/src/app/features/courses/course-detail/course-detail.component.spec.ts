import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { Course } from '@core/models/course.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { QrCodeService } from '@core/services/qr-code.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { CourseDetailComponent } from './course-detail.component';

describe('CourseDetailComponent', () => {
  let service: jasmine.SpyObj<CourseService>;
  let lookup: jasmine.SpyObj<LookupService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const item: Course = {
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
    scheduleOff: '2036-01-01',
    hour: 24,
    listPrice: 30000,
    learningCredit: 3.5,
    material: '原廠教材',
    objective: '第一行\n第二行',
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
    certificationPkids: [5, 99],
    jobCategoryPkids: [1]
  };

  async function setup(id = '1'): Promise<ComponentFixture<CourseDetailComponent>> {
    await TestBed.configureTestingModule({
      imports: [CourseDetailComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CourseDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['getById', 'delete']);
    service.getById.and.returnValue(of(item));
    service.delete.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['certifications', 'jobCategories']);
    lookup.certifications.and.returnValue(of([{ pkid: 5, label: 'Microsoft Azure Administrator Associate' }]));
    lookup.jobCategories.and.returnValue(of([{ pkid: 1, label: '系統管理' }]));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRecord']);
    rowAudits.getForRecord.and.returnValue(of([
      { dateTime: '2026-09-07T01:02:03', userName: 'system', actionType: 'UPDATE', actionDesc: 'Title' }
    ]));
  });

  it('loads the record and the two N-N lookups from the :id route param', async () => {
    await setup('1');
    expect(service.getById).toHaveBeenCalledWith(1);
    expect(lookup.certifications).toHaveBeenCalled();
    expect(lookup.jobCategories).toHaveBeenCalled();
  });

  it('renders the basic fields with FK labels as links', async () => {
    const fixture = await setup();
    const host = fixture.nativeElement as HTMLElement;
    const text = host.textContent ?? '';

    expect(text).toContain('AZ-104');
    expect(text).toContain('AZ104');
    expect(text).toContain('Azure 管理員');
    expect(text).toContain('Microsoft Azure Administrator');
    expect(text).toContain('2026-01-01');
    expect(text).toContain('30,000');
    expect(text).toContain('3.5');
    expect(text).toContain('是');

    const links = Array.from(host.querySelectorAll('a.fk-link')).map(a => a.textContent?.trim());
    expect(links).toEqual(['Microsoft', '雲端', '已發布']);
  });

  it('shows a dash for a missing course group and empty text blocks', async () => {
    service.getById.and.returnValue(of({ ...item, courseGroupPkid: null, courseGroupDescription: null }));
    const fixture = await setup();
    const host = fixture.nativeElement as HTMLElement;

    expect(host.querySelectorAll('a.fk-link').length).toBe(2);
    expect(host.textContent).toContain('—');
  });

  it('resolves certification and job-category labels, falling back to #pkid', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Microsoft Azure Administrator Associate、#99');
    expect(text).toContain('系統管理');
  });

  it('renders the four primary-foreign link buttons', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('查看課程問答');
    expect(text).toContain('查看相關連結');
    expect(text).toContain('查看熱門課程');
    expect(text).toContain('查看推薦課程');
  });

  it('shows the row-audit badge for the record', async () => {
    const fixture = await setup();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rowAudits.getForRecord).toHaveBeenCalledWith('Course', 1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Update by system');
  });

  describe('QR code (基本資料)', () => {
    const expectedUrl = 'https://www.uuu.com.tw/Course/Show/1/AZ-104';

    async function settle(fixture: ComponentFixture<CourseDetailComponent>): Promise<void> {
      await fixture.whenStable();
      await new Promise(resolve => setTimeout(resolve, 0));
      fixture.detectChanges();
    }

    it('encodes the public course URL built from pkid and courseId', async () => {
      const toCanvas = spyOn(QrCodeService.prototype, 'toCanvas').and.callThrough();
      const fixture = await setup();
      await settle(fixture);
      const host = fixture.nativeElement as HTMLElement;

      expect(toCanvas).toHaveBeenCalledWith(jasmine.any(HTMLCanvasElement), expectedUrl, jasmine.anything());
      expect(host.querySelector('.basic-info figure.qr-code')?.getAttribute('data-qr-text')).toBe(expectedUrl);
      expect(host.querySelector<HTMLAnchorElement>('.basic-info__qr-link')?.href).toBe(expectedUrl);
    });

    it('uses a different record\'s pkid and courseId in the URL', async () => {
      service.getById.and.returnValue(of({ ...item, pkid: 42, courseId: 'MS-900' }));
      const fixture = await setup('42');
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).querySelector('figure.qr-code')?.getAttribute('data-qr-text'))
        .toBe('https://www.uuu.com.tw/Course/Show/42/MS-900');
    });

    it('shows the courseId as the QR code title', async () => {
      const fixture = await setup();
      await settle(fixture);

      const caption = (fixture.nativeElement as HTMLElement).querySelector('figure.qr-code figcaption');
      expect(caption?.textContent?.trim()).toBe('AZ-104');
    });

    it('downloads the QR code as a PNG named after the courseId', async () => {
      const fixture = await setup();
      await settle(fixture);
      let clicked: HTMLAnchorElement | undefined;
      spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function (this: HTMLAnchorElement) {
        clicked = this;
      });

      const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.qr-code__download button')!;
      expect(button.disabled).toBeFalse();
      button.click();

      expect(clicked).toBeDefined();
      expect(clicked!.download).toBe('AZ-104.png');
      expect(clicked!.href.startsWith('data:image/png;base64,')).toBeTrue();
    });
  });

  it('shows a not-found message on 404', async () => {
    service.getById.and.returnValue(throwError(() => ({ status: 404 })));
    const fixture = await setup('99');

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('找不到主代碼 99');
  });

  describe('列印PDF', () => {
    const printButton = (fixture: ComponentFixture<CourseDetailComponent>) =>
      (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.toolbar-print button')!;

    it('is disabled until the record has loaded', async () => {
      service.getById.and.returnValue(throwError(() => ({ status: 404 })));
      const fixture = await setup('99');

      expect(printButton(fixture).disabled).toBeTrue();
    });

    it('opens the print view in a new tab, without noopener, so the session carries over', async () => {
      const open = spyOn(window, 'open').and.returnValue(null);
      const fixture = await setup();

      const button = printButton(fixture);
      expect(button.disabled).toBeFalse();
      button.click();

      expect(open).toHaveBeenCalledOnceWith('/course/courses/1/print', '_blank');
      expect(open.calls.mostRecent().args.length).toBe(2);
    });
  });

  it('asks for confirmation with pkid and courseId before deleting', async () => {
    const fixture = await setup();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const confirmation = TestBed.inject(ConfirmationService);
    spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>1</b>');
      expect(options.message).toContain('「AZ-104」');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: () => void }).confirmDelete();

    expect(service.delete).toHaveBeenCalledWith(1);
    expect(navigate).toHaveBeenCalledWith(['/course/courses']);
  });
});
