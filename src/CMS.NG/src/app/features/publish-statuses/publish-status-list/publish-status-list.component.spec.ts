import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { PublishStatus } from '@core/models/publish-status.model';
import { PublishStatusService } from '@core/services/publish-status.service';
import {
  PUBLISH_STATUS_LIST_FILTERS_KEY,
  PUBLISH_STATUS_LIST_PAGE_KEY,
  PublishStatusListComponent
} from './publish-status-list.component';

describe('PublishStatusListComponent', () => {
  let fixture: ComponentFixture<PublishStatusListComponent>;
  let service: jasmine.SpyObj<PublishStatusService>;

  const items: PublishStatus[] = [
    { pkid: 1, description: '草稿', isDraft: true, isPublished: false, isDiscontinued: false },
    { pkid: 2, description: '已發布', isDraft: false, isPublished: true, isDiscontinued: false }
  ];

  beforeEach(async () => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['query', 'delete']);
    service.query.and.returnValue(of(items));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PublishStatusListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: PublishStatusService, useValue: service }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusListComponent);
    fixture.detectChanges();
  });

  afterEach(() => sessionStorage.clear());

  it('creates and queries the service on init', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('renders one row per item with the description', () => {
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('草稿');
    expect(rows[1].textContent).toContain('已發布');
  });

  it('renders the 查看課程 / 查看活動 link buttons on each row', () => {
    const firstRow = (fixture.nativeElement as HTMLElement).querySelector('tbody tr')!;
    expect(firstRow.textContent).toContain('查看課程');
    expect(firstRow.textContent).toContain('查看活動');
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
      filters: { keyword: string | null; isDraft: boolean | null; isPublished: boolean | null };
      activeFilterCount: number;
    };
    const filterButton = () =>
      Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
        .find(b => b.textContent?.includes('搜尋條件'))!;

    expect(component.activeFilterCount).toBe(0);
    expect(filterButton().querySelector('.p-badge')).toBeNull();

    component.filters.keyword = '草';
    component.filters.isDraft = true;
    component.filters.isPublished = false;
    fixture.detectChanges();

    expect(component.activeFilterCount).toBe(3);
    expect(filterButton().querySelector('.p-badge')?.textContent?.trim()).toBe('3');
  });

  it('persists filters and resets paging to the first page on search', () => {
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null };
      first: number;
      search: () => void;
    };
    component.first = 40;
    component.filters.keyword = '草';

    component.search();

    expect(component.first).toBe(0);
    expect(JSON.parse(sessionStorage.getItem(PUBLISH_STATUS_LIST_FILTERS_KEY)!).keyword).toBe('草');
    expect(JSON.parse(sessionStorage.getItem(PUBLISH_STATUS_LIST_PAGE_KEY)!).first).toBe(0);
    expect(service.query).toHaveBeenCalledTimes(2);
    expect(service.query.calls.mostRecent().args[0].keyword).toBe('草');
  });

  it('restores saved filters from session storage on init', async () => {
    sessionStorage.setItem(PUBLISH_STATUS_LIST_FILTERS_KEY, JSON.stringify({ keyword: '已', isPublished: true }));

    const second = TestBed.createComponent(PublishStatusListComponent);
    second.detectChanges();

    const args = service.query.calls.mostRecent().args[0];
    expect(args.keyword).toBe('已');
    expect(args.isPublished).toBeTrue();
  });

  it('asks for confirmation with pkid and description before deleting', () => {
    const confirmation = TestBed.inject(ConfirmationService);
    const confirmSpy = spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>1</b>');
      expect(options.message).toContain('「草稿」');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: (i: PublishStatus) => void }).confirmDelete(items[0]);

    expect(confirmSpy).toHaveBeenCalled();
    expect(service.delete).toHaveBeenCalledWith(1);
    expect(service.query).toHaveBeenCalledTimes(2);
  });
});
