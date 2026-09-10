import { ApplicationRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { Course } from '@core/models/course.model';
import { PublishStatus } from '@core/models/publish-status.model';
import { CourseService } from '@core/services/course.service';
import { PublishStatusService } from '@core/services/publish-status.service';
import { QrCodeService } from '@core/services/qr-code.service';
import {
  CoursePrintComponent,
  PRINT_COURSE_ID_PROPERTY,
  PRINT_DATE_PROPERTY,
  cssString
} from './course-print.component';

describe('CoursePrintComponent', () => {
  let courses: jasmine.SpyObj<CourseService>;
  let statuses: jasmine.SpyObj<PublishStatusService>;
  let print: jasmine.Spy;
  let toCanvas: jasmine.Spy;

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
    publishStatusPkid: 2,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
    hour: 24,
    listPrice: 30000,
    learningCredit: 3.5,
    material: '原廠教材',
    objective: '第一行\n第二行',
    target: null,
    prerequisites: '   \n ',
    outline: '模組 1\n模組 2',
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    partnerName: 'Microsoft',
    courseGroupDescription: '雲端',
    publishStatusDescription: '已發布',
    certificationPkids: [5],
    jobCategoryPkids: [1]
  };

  const status = (isPublished: boolean): PublishStatus => ({
    pkid: 2,
    description: isPublished ? '已發布' : '草稿',
    isDraft: !isPublished,
    isPublished,
    isDiscontinued: false
  });

  /** Lets the async QR render, change detection and the `afterNextRender` print hook run. */
  async function settle(fixture: ComponentFixture<CoursePrintComponent>): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();
    TestBed.inject(ApplicationRef).tick();
  }

  async function setup(id = '1'): Promise<ComponentFixture<CoursePrintComponent>> {
    await TestBed.configureTestingModule({
      imports: [CoursePrintComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: CourseService, useValue: courses },
        { provide: PublishStatusService, useValue: statuses },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CoursePrintComponent);
    await settle(fixture);
    return fixture;
  }

  const text = (fixture: ComponentFixture<CoursePrintComponent>) => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const headings = (fixture: ComponentFixture<CoursePrintComponent>) =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.course-print__section h2')).map(h => h.textContent?.trim());

  beforeEach(() => {
    courses = jasmine.createSpyObj<CourseService>('CourseService', ['getById']);
    courses.getById.and.returnValue(of(item));
    statuses = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['getById']);
    statuses.getById.and.returnValue(of(status(true)));
    print = spyOn(window, 'print');
    toCanvas = spyOn(QrCodeService.prototype, 'toCanvas').and.callThrough();
  });

  afterEach(() => {
    document.documentElement.style.removeProperty(PRINT_COURSE_ID_PROPERTY);
    document.documentElement.style.removeProperty(PRINT_DATE_PROPERTY);
  });

  it('loads the course and its publish status from the :id route param', async () => {
    await setup('1');
    expect(courses.getById).toHaveBeenCalledWith(1);
    expect(statuses.getById).toHaveBeenCalledWith(2);
  });

  it('renders only the customer field set', async () => {
    const fixture = await setup();
    const t = text(fixture);

    expect(t).toContain('Azure 管理員');
    expect(t).toContain('Microsoft Azure Administrator');
    expect(t).toContain('AZ-104');
    expect(t).toContain('AZ104');
    expect(t).toContain('Microsoft');
    expect(t).toContain('雲端');
    expect(t).toContain('NT$ 30,000');
    expect(t).toContain('3.5');
    expect(t).toContain('是');

    for (const internal of ['主代碼', '上架狀態', '顯示順序', '友善網址', '上架日期', '認證', '職務類別', '相關資料', '異動紀錄']) {
      expect(t).withContext(internal).not.toContain(internal);
    }
    expect(t).not.toContain('2026-01-01');
    expect(t).not.toContain('azure-administrator');
  });

  it('omits empty (null or whitespace-only) content sections and keeps print order', async () => {
    const fixture = await setup();
    expect(headings(fixture)).toEqual(['課程目標', '教材', '課程大綱']);
    expect((fixture.nativeElement as HTMLElement).querySelector('.course-print__section--flow h2')?.textContent).toBe('課程大綱');
  });

  it('omits 官方課程名稱 and 課程群組 when empty', async () => {
    courses.getById.and.returnValue(of({ ...item, officialTitle: '  ', courseGroupPkid: null, courseGroupDescription: null }));
    const fixture = await setup();
    const host = fixture.nativeElement as HTMLElement;

    expect(host.querySelector('.course-print__official')).toBeNull();
    expect(text(fixture)).not.toContain('課程群組');
  });

  it('shows the QR code with the public URL, without a download button, for a published course', async () => {
    const fixture = await setup();
    const host = fixture.nativeElement as HTMLElement;

    expect(host.querySelector('figure.qr-code')?.getAttribute('data-qr-text')).toBe('https://www.uuu.com.tw/Course/Show/1/AZ-104');
    expect(text(fixture)).toContain('https://www.uuu.com.tw/Course/Show/1/AZ-104');
    expect(host.querySelector('.qr-code__download')).toBeNull();
  });

  it('omits the QR code for an unpublished course and prints without waiting for it', async () => {
    statuses.getById.and.returnValue(of(status(false)));
    const fixture = await setup();

    expect((fixture.nativeElement as HTMLElement).querySelector('figure.qr-code')).toBeNull();
    expect(toCanvas).not.toHaveBeenCalled();
    expect(print).toHaveBeenCalledTimes(1);
  });

  it('treats a failed publish-status lookup as unpublished and still prints', async () => {
    statuses.getById.and.returnValue(throwError(() => ({ status: 500 })));
    const fixture = await setup();

    expect((fixture.nativeElement as HTMLElement).querySelector('figure.qr-code')).toBeNull();
    expect(text(fixture)).toContain('Azure 管理員');
    expect(text(fixture)).not.toContain('無法取得課程資料');
    expect(print).toHaveBeenCalledTimes(1);
  });

  it('sets the document title to "{courseId} {title}" for the default PDF file name', async () => {
    await setup();
    expect(document.title).toBe('AZ-104 Azure 管理員');
  });

  it('exposes the footer values as quoted CSS strings on the root element', async () => {
    courses.getById.and.returnValue(of({ ...item, courseId: 'A"Z\\1' }));
    await setup();
    const style = document.documentElement.style;

    expect(style.getPropertyValue(PRINT_COURSE_ID_PROPERTY)).toBe('"A\\"Z\\\\1"');
    expect(style.getPropertyValue(PRINT_DATE_PROPERTY)).toMatch(/^"\d{4}-\d{2}-\d{2}"$/);
  });

  it('quotes CSS strings with " and \\ escaped', () => {
    expect(cssString('AZ-104')).toBe('"AZ-104"');
    expect(cssString('a"b\\c')).toBe('"a\\"b\\\\c"');
  });

  it('prints exactly once, after the course and the QR code are ready', async () => {
    const fixture = await setup();
    expect(toCanvas).toHaveBeenCalledTimes(1);
    expect(print).toHaveBeenCalledTimes(1);

    await settle(fixture);
    expect(print).toHaveBeenCalledTimes(1);
  });

  it('still prints once when the QR code fails to encode', async () => {
    toCanvas.and.rejectWith(new Error('too long'));
    const fixture = await setup();

    expect(text(fixture)).toContain('無法產生 QR Code');
    expect(print).toHaveBeenCalledTimes(1);
  });

  it('does not print before the QR code has settled', async () => {
    let resolveRender!: () => void;
    toCanvas.and.returnValue(new Promise<void>(resolve => (resolveRender = resolve)));
    const fixture = await setup();
    expect(print).not.toHaveBeenCalled();

    resolveRender();
    await settle(fixture);
    expect(print).toHaveBeenCalledTimes(1);
  });

  it('re-opens the print dialog from the 列印 button', async () => {
    const fixture = await setup();
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.course-print__actions button')!.click();

    expect(print).toHaveBeenCalledTimes(2);
  });

  it('shows the not-found text and never prints on 404', async () => {
    courses.getById.and.returnValue(throwError(() => ({ status: 404 })));
    const fixture = await setup('99');

    expect(text(fixture)).toContain('找不到主代碼 99 的課程');
    expect((fixture.nativeElement as HTMLElement).querySelector('.course-print__sheet')).toBeNull();
    expect(print).not.toHaveBeenCalled();
  });

  it('shows the plain error text and never prints on other failures', async () => {
    courses.getById.and.returnValue(throwError(() => ({ status: 500 })));
    const fixture = await setup();

    expect(text(fixture)).toContain('無法取得課程資料');
    expect(print).not.toHaveBeenCalled();
  });

  it('clears the footer custom properties when destroyed', async () => {
    const fixture = await setup();
    expect(document.documentElement.style.getPropertyValue(PRINT_COURSE_ID_PROPERTY)).toBe('"AZ-104"');

    fixture.destroy();

    expect(document.documentElement.style.getPropertyValue(PRINT_COURSE_ID_PROPERTY)).toBe('');
    expect(document.documentElement.style.getPropertyValue(PRINT_DATE_PROPERTY)).toBe('');
  });
});
