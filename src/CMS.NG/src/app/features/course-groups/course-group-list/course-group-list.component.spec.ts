import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { CourseGroup } from '@core/models/course-group.model';
import { CourseGroupService } from '@core/services/course-group.service';
import {
  COURSE_GROUP_LIST_FILTERS_KEY,
  COURSE_GROUP_LIST_PAGE_KEY,
  CourseGroupListComponent
} from './course-group-list.component';

describe('CourseGroupListComponent', () => {
  let fixture: ComponentFixture<CourseGroupListComponent>;
  let service: jasmine.SpyObj<CourseGroupService>;

  const items: CourseGroup[] = [
    { pkid: 2, description: '資訊安全' },
    { pkid: 1, description: '雲端運算' }
  ];

  beforeEach(async () => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['query', 'delete']);
    service.query.and.returnValue(of(items));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [CourseGroupListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: CourseGroupService, useValue: service }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseGroupListComponent);
    fixture.detectChanges();
  });

  afterEach(() => sessionStorage.clear());

  it('creates and queries the service on init', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('renders one row per item with pkid and description', () => {
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('2');
    expect(rows[0].textContent).toContain('資訊安全');
    expect(rows[1].textContent).toContain('雲端運算');
  });

  it('renders the 查看課程 / 查看夥伴課程群組 link buttons on each row', () => {
    const firstRow = (fixture.nativeElement as HTMLElement).querySelector('tbody tr')!;
    expect(firstRow.textContent).toContain('查看課程');
    expect(firstRow.textContent).toContain('查看夥伴課程群組');
  });

  it('opens the filter drawer from the 搜尋條件 button', () => {
    const component = fixture.componentInstance as unknown as { filterVisible: () => boolean };
    expect(component.filterVisible()).toBeFalse();

    const button = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(b => b.textContent?.includes('搜尋條件'))!;
    button.click();
    fixture.detectChanges();

    expect(component.filterVisible()).toBeTrue();
  });

  it('shows the number of active filters on the 搜尋條件 button', () => {
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null };
      activeFilterCount: number;
    };
    const filterButton = () =>
      Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
        .find(b => b.textContent?.includes('搜尋條件'))!;

    expect(component.activeFilterCount).toBe(0);
    expect(filterButton().querySelector('.p-badge')).toBeNull();

    component.filters.keyword = '雲端';
    fixture.detectChanges();

    expect(component.activeFilterCount).toBe(1);
    expect(filterButton().querySelector('.p-badge')?.textContent?.trim()).toBe('1');
  });

  it('persists filters and resets paging to the first page on search', () => {
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null };
      first: number;
      search: () => void;
    };
    component.first = 40;
    component.filters.keyword = '雲端';

    component.search();

    expect(component.first).toBe(0);
    expect(JSON.parse(sessionStorage.getItem(COURSE_GROUP_LIST_FILTERS_KEY)!).keyword).toBe('雲端');
    expect(JSON.parse(sessionStorage.getItem(COURSE_GROUP_LIST_PAGE_KEY)!).first).toBe(0);
    expect(service.query).toHaveBeenCalledTimes(2);
    expect(service.query.calls.mostRecent().args[0].keyword).toBe('雲端');
  });

  it('restores saved filters from session storage on init', async () => {
    sessionStorage.setItem(COURSE_GROUP_LIST_FILTERS_KEY, JSON.stringify({ keyword: '資安' }));

    const second = TestBed.createComponent(CourseGroupListComponent);
    second.detectChanges();

    expect(service.query.calls.mostRecent().args[0].keyword).toBe('資安');
  });

  it('asks for confirmation with pkid and description before deleting', () => {
    const confirmation = TestBed.inject(ConfirmationService);
    const confirmSpy = spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>2</b>');
      expect(options.message).toContain('「資訊安全」');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: (i: CourseGroup) => void }).confirmDelete(items[0]);

    expect(confirmSpy).toHaveBeenCalled();
    expect(service.delete).toHaveBeenCalledWith(2);
    expect(service.query).toHaveBeenCalledTimes(2);
  });
});
