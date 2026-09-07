import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { Course } from '@core/models/course.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import {
  COURSE_LIST_FILTERS_KEY,
  COURSE_LIST_PAGE_KEY,
  CourseListComponent
} from './course-list.component';

describe('CourseListComponent', () => {
  let service: jasmine.SpyObj<CourseService>;
  let lookup: jasmine.SpyObj<LookupService>;

  const base: Course = {
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
    material: null,
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
    certificationPkids: [],
    jobCategoryPkids: []
  };

  const items: Course[] = [
    base,
    { ...base, pkid: 2, displayOrder: 20, courseId: 'CCNA', prodCourseId: 'CCNA1', title: 'CCNA 認證班', partnerName: 'Cisco', courseGroupPkid: null, courseGroupDescription: null, canRepeat: false, listPrice: 45000, learningCredit: 4 }
  ];

  async function setup(queryParams: Record<string, string> = {}): Promise<ComponentFixture<CourseListComponent>> {
    await TestBed.configureTestingModule({
      imports: [CourseListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CourseListComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<CourseService>('CourseService', ['query', 'delete']);
    service.query.and.returnValue(of(items));
    service.delete.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['partners', 'courseGroups', 'publishStatuses']);
    lookup.partners.and.returnValue(of([{ pkid: 1, label: 'Microsoft' }, { pkid: 2, label: 'Cisco' }]));
    lookup.courseGroups.and.returnValue(of([{ pkid: 2, label: '雲端' }]));
    lookup.publishStatuses.and.returnValue(of([{ pkid: 1, label: '已發布' }]));
  });

  afterEach(() => sessionStorage.clear());

  it('creates, loads the three filter lookups and queries the service on init', async () => {
    const fixture = await setup();

    expect(fixture.componentInstance).toBeTruthy();
    expect(service.query).toHaveBeenCalledTimes(1);
    expect(lookup.partners).toHaveBeenCalled();
    expect(lookup.courseGroups).toHaveBeenCalled();
    expect(lookup.publishStatuses).toHaveBeenCalled();
  });

  it('renders the requested columns with JOINed partner / group / status labels', async () => {
    const fixture = await setup();
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');

    expect(rows.length).toBe(2);
    const first = rows[0].textContent ?? '';
    expect(first).toContain('AZ-104');
    expect(first).toContain('AZ104');
    expect(first).toContain('Azure 管理員');
    expect(first).toContain('Microsoft');
    expect(first).toContain('雲端');
    expect(first).toContain('已發布');
    expect(first).toContain('2026-01-01');
    expect(first).toContain('2036-01-01');
    expect(first).toContain('30,000');
    expect(first).toContain('3.5');
    expect(first).toContain('是');

    const second = rows[1].textContent ?? '';
    expect(second).toContain('Cisco');
    expect(second).toContain('—');
    expect(second).toContain('否');
    expect(second).toContain('4.0');
  });

  it('renders the column headers in the requested order', async () => {
    const fixture = await setup();
    const headers = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('thead th'))
      .map(th => th.textContent?.trim() ?? '');

    expect(headers).toEqual([
      '主代碼', '顯示順序', '簡介代碼', '科目代碼', '課程名稱', '原廠', '課程群組', '上架狀態',
      '上架日期', '下架日期', '時數', '定價', '點數', '允許重聽', '操作'
    ]);
  });

  it('applies an incoming partnerPkid query param over the saved filter', async () => {
    sessionStorage.setItem(COURSE_LIST_FILTERS_KEY, JSON.stringify({ keyword: 'AZ', partnerPkid: 9 }));

    await setup({ partnerPkid: '2' });

    const sent = service.query.calls.mostRecent().args[0];
    expect(sent.partnerPkid).toBe(2);
    expect(sent.keyword).toBe('AZ');
  });

  it('applies an incoming courseGroupPkid query param', async () => {
    await setup({ courseGroupPkid: '5' });

    expect(service.query.calls.mostRecent().args[0].courseGroupPkid).toBe(5);
  });

  it('counts active filters for the 搜尋條件 badge', async () => {
    const fixture = await setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null; partnerPkid: number | null; canRepeat: boolean | null; scheduleOnFrom: string | null };
      activeFilterCount: number;
    };

    expect(component.activeFilterCount).toBe(0);

    component.filters.keyword = 'AZ';
    component.filters.partnerPkid = 1;
    component.filters.canRepeat = false;
    component.filters.scheduleOnFrom = '2026-01-01';
    fixture.detectChanges();

    expect(component.activeFilterCount).toBe(4);
    const filterButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(b => b.textContent?.includes('搜尋條件'))!;
    expect(filterButton.querySelector('.p-badge')?.textContent?.trim()).toBe('4');
  });

  it('serialises date-range pickers to ISO strings, persists filters and resets paging on search', async () => {
    const fixture = await setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null; scheduleOnFrom: string | null; scheduleOffTo: string | null };
      dateFilters: { scheduleOnFrom: Date | null; scheduleOffTo: Date | null };
      first: number;
      search: () => void;
    };
    component.first = 40;
    component.filters.keyword = 'AZ';
    component.dateFilters.scheduleOnFrom = new Date(2026, 0, 15);
    component.dateFilters.scheduleOffTo = new Date(2036, 11, 31);

    component.search();

    expect(component.first).toBe(0);
    expect(component.filters.scheduleOnFrom).toBe('2026-01-15');
    expect(component.filters.scheduleOffTo).toBe('2036-12-31');
    const saved = JSON.parse(sessionStorage.getItem(COURSE_LIST_FILTERS_KEY)!);
    expect(saved.keyword).toBe('AZ');
    expect(saved.scheduleOnFrom).toBe('2026-01-15');
    expect(JSON.parse(sessionStorage.getItem(COURSE_LIST_PAGE_KEY)!).first).toBe(0);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('restores saved date filters into the pickers on init', async () => {
    sessionStorage.setItem(COURSE_LIST_FILTERS_KEY, JSON.stringify({ scheduleOnFrom: '2026-03-01' }));

    const fixture = await setup();
    const component = fixture.componentInstance as unknown as { dateFilters: { scheduleOnFrom: Date | null } };

    expect(component.dateFilters.scheduleOnFrom?.getMonth()).toBe(2);
    expect(service.query.calls.mostRecent().args[0].scheduleOnFrom).toBe('2026-03-01');
  });

  it('asks for confirmation with pkid and courseId before deleting', async () => {
    const fixture = await setup();
    const confirmation = TestBed.inject(ConfirmationService);
    const confirmSpy = spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>1</b>');
      expect(options.message).toContain('「AZ-104」');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: (i: Course) => void }).confirmDelete(items[0]);

    expect(confirmSpy).toHaveBeenCalled();
    expect(service.delete).toHaveBeenCalledWith(1);
    expect(service.query).toHaveBeenCalledTimes(2);
  });
});
