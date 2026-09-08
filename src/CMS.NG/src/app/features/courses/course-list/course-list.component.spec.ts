import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { Course, CourseRequest } from '@core/models/course.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { CellEdit, EditableCourseField } from './course-inline-edit';
import {
  COURSE_LIST_FILTERS_KEY,
  COURSE_LIST_PAGE_KEY,
  CourseListComponent
} from './course-list.component';

/** Protected members reached by the inline-edit tests. */
interface InlineEditApi {
  cellEdit: CellEdit | null;
  items: () => Course[];
  startEdit: (item: Course, field: string) => void;
  commit: () => void;
  cancelEdit: () => void;
}

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

  /** What GET /{id} returns for row 1: same scalars plus the N-N lists the list endpoint leaves empty. */
  const fullRow: Course = { ...base, material: '講義', certificationPkids: [7, 9], jobCategoryPkids: [3] };

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
    service = jasmine.createSpyObj<CourseService>('CourseService', ['query', 'delete', 'getById', 'update']);
    service.query.and.returnValue(of(items));
    service.delete.and.returnValue(of(void 0));
    service.getById.and.returnValue(of(fullRow));
    service.update.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['partners', 'courseGroups', 'publishStatuses']);
    lookup.partners.and.returnValue(of([{ pkid: 1, label: 'Microsoft' }, { pkid: 2, label: 'Cisco' }]));
    lookup.courseGroups.and.returnValue(of([{ pkid: 2, label: '雲端' }]));
    lookup.publishStatuses.and.returnValue(of([{ pkid: 1, label: '已發布' }, { pkid: 2, label: '草稿' }]));
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

  describe('in-place editing', () => {
    let fixture: ComponentFixture<CourseListComponent>;
    let component: InlineEditApi;

    function cell(field: string, rowIndex = 0): HTMLTableCellElement {
      const row = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')[rowIndex];
      return row.querySelector<HTMLTableCellElement>(`td[data-field="${field}"]`)!;
    }

    function editorIn(field: string, rowIndex = 0): HTMLElement | null {
      return cell(field, rowIndex).querySelector<HTMLElement>('input, p-select, p-datepicker, p-checkbox');
    }

    function errorIn(field: string, rowIndex = 0): string | null {
      return cell(field, rowIndex).querySelector('.cell-error')?.textContent?.trim() ?? null;
    }

    /** Opens the editor by double-clicking and waits for ngModel to write the initial value into the input. */
    async function openByDoubleClick(field: string, rowIndex = 0): Promise<void> {
      cell(field, rowIndex).dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    }

    function typeAndBlur(field: string, value: string, rowIndex = 0): void {
      const input = cell(field, rowIndex).querySelector<HTMLInputElement>('input')!;
      input.value = value;
      input.dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
      input.dispatchEvent(new Event('blur', { bubbles: true }));
      fixture.detectChanges();
    }

    /** Opens the editor programmatically and commits a draft value (for editors that are not plain inputs). */
    function commitDraft(field: EditableCourseField, value: CellEdit['value'], rowIndex = 0): void {
      component.startEdit(component.items()[rowIndex], field);
      component.cellEdit!.value = value;
      component.commit();
      fixture.detectChanges();
    }

    beforeEach(async () => {
      fixture = await setup();
      component = fixture.componentInstance as unknown as InlineEditApi;
    });

    it('does not enter edit mode on a single click', () => {
      cell('title').dispatchEvent(new MouseEvent('click', { bubbles: true }));
      fixture.detectChanges();

      expect(component.cellEdit).toBeNull();
      expect(editorIn('title')).toBeNull();
      expect(cell('title').textContent?.trim()).toBe('Azure 管理員');
    });

    it('enters edit mode on double-click, showing a text input pre-filled with the cell value', async () => {
      await openByDoubleClick('title');

      expect(component.cellEdit).toEqual({ pkid: 1, field: 'title', value: 'Azure 管理員', error: null });
      const input = cell('title').querySelector<HTMLInputElement>('input')!;
      expect(input).not.toBeNull();
      expect(input.value).toBe('Azure 管理員');
      expect(cell('title').classList).toContain('editing');
    });

    it('uses the editor matching each column type', async () => {
      await openByDoubleClick('hour');
      expect(cell('hour').querySelector('p-inputnumber')).not.toBeNull();

      await openByDoubleClick('scheduleOn');
      expect(cell('scheduleOn').querySelector('p-datepicker')).not.toBeNull();

      await openByDoubleClick('publishStatusPkid');
      expect(cell('publishStatusPkid').querySelector('p-select')).not.toBeNull();

      await openByDoubleClick('canRepeat');
      expect(cell('canRepeat').querySelector('p-checkbox')).not.toBeNull();
    });

    it('keeps 主代碼, 原廠 and 課程群組 read-only even on double-click', async () => {
      for (const field of ['pkid', 'partnerName', 'courseGroupDescription']) {
        await openByDoubleClick(field);

        expect(component.cellEdit).withContext(field).toBeNull();
        expect(editorIn(field)).withContext(field).toBeNull();
        expect(cell(field).classList).withContext(field).not.toContain('editable');
      }

      component.startEdit(component.items()[0], 'partnerName');
      expect(component.cellEdit).toBeNull();
    });

    it('persists the edited value on blur through getById + update, keeping the N-N lists from the server', async () => {
      await openByDoubleClick('title');

      typeAndBlur('title', '  Azure 系統管理員 ');

      expect(service.getById).toHaveBeenCalledWith(1);
      expect(service.update).toHaveBeenCalledTimes(1);
      const sent = service.update.calls.mostRecent().args[0] as CourseRequest & { partnerName?: string };
      expect(sent.pkid).toBe(1);
      expect(sent.title).toBe('Azure 系統管理員');
      expect(sent.material).toBe('講義');
      expect(sent.certificationPkids).toEqual([7, 9]);
      expect(sent.jobCategoryPkids).toEqual([3]);
      expect(sent.partnerName).toBeUndefined();

      expect(component.cellEdit).toBeNull();
      expect(editorIn('title')).toBeNull();
      expect(cell('title').textContent?.trim()).toBe('Azure 系統管理員');
      expect(component.items()[0].title).toBe('Azure 系統管理員');
    });

    it('closes without calling the API when the value is unchanged on blur', async () => {
      await openByDoubleClick('courseId');

      typeAndBlur('courseId', 'AZ-104');

      expect(service.update).not.toHaveBeenCalled();
      expect(service.getById).not.toHaveBeenCalled();
      expect(component.cellEdit).toBeNull();
    });

    it('updates the 上架狀態 label alongside the pkid when the dropdown value is committed', () => {
      commitDraft('publishStatusPkid', 2);

      expect(service.update.calls.mostRecent().args[0].publishStatusPkid).toBe(2);
      expect(component.items()[0].publishStatusDescription).toBe('草稿');
      expect(cell('publishStatusPkid').textContent?.trim()).toBe('草稿');
    });

    it('blocks clearing a required field: inline error, cell stays in edit mode, nothing saved', async () => {
      await openByDoubleClick('title');

      typeAndBlur('title', '   ');

      expect(service.update).not.toHaveBeenCalled();
      expect(component.cellEdit?.error).toBe('請輸入課程名稱（最多 200 字）');
      expect(errorIn('title')).toBe('請輸入課程名稱（最多 200 字）');
      expect(editorIn('title')).not.toBeNull();
      expect(component.items()[0].title).toBe('Azure 管理員');
    });

    it('blocks non-ASCII 簡介代碼 with the same rule as the form', async () => {
      await openByDoubleClick('courseId');

      typeAndBlur('courseId', 'AZ 104');

      expect(service.update).not.toHaveBeenCalled();
      expect(errorIn('courseId')).toBe('只能輸入英數字與符號（不可含空白或中文）');
      expect(editorIn('courseId')).not.toBeNull();
    });

    it('blocks negative or missing numbers in 時數 / 定價 / 點數', () => {
      commitDraft('hour', -1);
      expect(errorIn('hour')).toBe('請輸入時數（0 以上）');
      expect(component.cellEdit?.field).toBe('hour');
      component.cancelEdit();

      commitDraft('listPrice', null);
      expect(errorIn('listPrice')).toBe('請輸入定價（0 以上）');
      component.cancelEdit();

      commitDraft('learningCredit', -0.5);
      expect(errorIn('learningCredit')).toBe('請輸入點數（0 以上）');
      component.cancelEdit();

      expect(service.update).not.toHaveBeenCalled();
      expect(component.items()[0].hour).toBe(24);
      expect(component.items()[0].listPrice).toBe(30000);
      expect(component.items()[0].learningCredit).toBe(3.5);
    });

    it('accepts zero for the numeric columns', () => {
      commitDraft('hour', 0);

      expect(errorIn('hour')).toBeNull();
      expect(service.update.calls.mostRecent().args[0].hour).toBe(0);
      expect(component.items()[0].hour).toBe(0);
    });

    it('blocks an invalid or cleared date', () => {
      commitDraft('scheduleOn', null);
      expect(errorIn('scheduleOn')).toBe('請選擇上架日期');
      component.cancelEdit();

      commitDraft('scheduleOff', new Date('not a date'));
      expect(errorIn('scheduleOff')).toBe('請選擇下架日期');
      component.cancelEdit();

      expect(service.update).not.toHaveBeenCalled();
    });

    it('blocks 上架日期 after 下架日期 and 下架日期 before 上架日期', () => {
      commitDraft('scheduleOn', new Date(2036, 0, 2));
      expect(errorIn('scheduleOn')).toBe('上架日期不可晚於下架日期');
      expect(editorIn('scheduleOn')).not.toBeNull();
      component.cancelEdit();

      commitDraft('scheduleOff', new Date(2025, 11, 31));
      expect(errorIn('scheduleOff')).toBe('下架日期不可早於上架日期');
      component.cancelEdit();

      expect(service.update).not.toHaveBeenCalled();
      expect(component.items()[0].scheduleOn).toBe('2026-01-01');
      expect(component.items()[0].scheduleOff).toBe('2036-01-01');
    });

    it('saves a date as yyyy-MM-dd when the range is valid', () => {
      commitDraft('scheduleOff', new Date(2030, 5, 30));

      expect(service.update.calls.mostRecent().args[0].scheduleOff).toBe('2030-06-30');
      expect(cell('scheduleOff').textContent?.trim()).toBe('2030-06-30');
    });

    it('reverts the cell and shows an error toast when the save fails', async () => {
      service.update.and.returnValue(throwError(() => ({ status: 500 })));
      const toast = spyOn(TestBed.inject(MessageService), 'add').and.callThrough();
      await openByDoubleClick('title');

      typeAndBlur('title', 'Broken');

      expect(service.update).toHaveBeenCalledTimes(1);
      expect(component.items()[0].title).toBe('Azure 管理員');
      expect(cell('title').textContent?.trim()).toBe('Azure 管理員');
      expect(component.cellEdit).toBeNull();
      expect(toast).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error', summary: '儲存失敗' }));
    });

    it('reverts the 上架狀態 label too when that save fails', () => {
      service.update.and.returnValue(throwError(() => ({ status: 404 })));

      commitDraft('publishStatusPkid', 2);

      expect(component.items()[0].publishStatusPkid).toBe(1);
      expect(component.items()[0].publishStatusDescription).toBe('已發布');
    });

    it('does not open another cell while the current one is invalid', () => {
      commitDraft('hour', -1);

      component.startEdit(component.items()[1], 'title');

      expect(component.cellEdit?.field).toBe('hour');
      expect(component.cellEdit?.pkid).toBe(1);
    });

    it('discards the draft on Escape', async () => {
      await openByDoubleClick('title');
      const input = cell('title').querySelector<HTMLInputElement>('input')!;
      input.value = 'Discarded';
      input.dispatchEvent(new Event('input', { bubbles: true }));

      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      fixture.detectChanges();

      expect(component.cellEdit).toBeNull();
      expect(service.update).not.toHaveBeenCalled();
      expect(cell('title').textContent?.trim()).toBe('Azure 管理員');
    });
  });
});
