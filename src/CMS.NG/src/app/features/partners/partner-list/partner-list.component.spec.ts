import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { Partner } from '@core/models/partner.model';
import { PartnerService } from '@core/services/partner.service';
import {
  PARTNER_LIST_FILTERS_KEY,
  PARTNER_LIST_PAGE_KEY,
  PartnerListComponent
} from './partner-list.component';

describe('PartnerListComponent', () => {
  let fixture: ComponentFixture<PartnerListComponent>;
  let service: jasmine.SpyObj<PartnerService>;

  const items: Partner[] = [
    { pkid: 1, name: 'Microsoft', appKey: 'MS', nameOnPartnerMenu: 'Microsoft 微軟', nameOnCourseDetailPage: '微軟', displayOrder: 10, imageFilename: 'microsoft.png' },
    { pkid: 2, name: 'Cisco', appKey: 'CISCO', nameOnPartnerMenu: 'Cisco 思科', nameOnCourseDetailPage: '思科', displayOrder: 20, imageFilename: null }
  ];

  beforeEach(async () => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['query', 'delete']);
    service.query.and.returnValue(of(items));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PartnerListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: PartnerService, useValue: service }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerListComponent);
    fixture.detectChanges();
  });

  afterEach(() => sessionStorage.clear());

  it('creates and queries the service on init', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('renders one row per item with the name and app key', () => {
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Microsoft');
    expect(rows[0].textContent).toContain('MS');
    expect(rows[1].textContent).toContain('Cisco');
  });

  it('shows a dash for a missing image filename', () => {
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows[0].textContent).toContain('microsoft.png');
    expect(rows[1].textContent).toContain('—');
  });

  it('renders the 查看課程 / 查看認證 link buttons on each row', () => {
    const firstRow = (fixture.nativeElement as HTMLElement).querySelector('tbody tr')!;
    expect(firstRow.textContent).toContain('查看課程');
    expect(firstRow.textContent).toContain('查看認證');
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

    component.filters.keyword = 'Micro';
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
    component.filters.keyword = 'Micro';

    component.search();

    expect(component.first).toBe(0);
    expect(JSON.parse(sessionStorage.getItem(PARTNER_LIST_FILTERS_KEY)!).keyword).toBe('Micro');
    expect(JSON.parse(sessionStorage.getItem(PARTNER_LIST_PAGE_KEY)!).first).toBe(0);
    expect(service.query).toHaveBeenCalledTimes(2);
    expect(service.query.calls.mostRecent().args[0].keyword).toBe('Micro');
  });

  it('restores saved filters from session storage on init', async () => {
    sessionStorage.setItem(PARTNER_LIST_FILTERS_KEY, JSON.stringify({ keyword: 'Cis' }));

    const second = TestBed.createComponent(PartnerListComponent);
    second.detectChanges();

    expect(service.query.calls.mostRecent().args[0].keyword).toBe('Cis');
  });

  it('asks for confirmation with pkid and name before deleting', () => {
    const confirmation = TestBed.inject(ConfirmationService);
    const confirmSpy = spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>1</b>');
      expect(options.message).toContain('「Microsoft」');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: (i: Partner) => void }).confirmDelete(items[0]);

    expect(confirmSpy).toHaveBeenCalled();
    expect(service.delete).toHaveBeenCalledWith(1);
    expect(service.query).toHaveBeenCalledTimes(2);
  });
});
