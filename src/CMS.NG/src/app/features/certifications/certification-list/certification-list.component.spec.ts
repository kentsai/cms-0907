import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { Certification } from '@core/models/certification.model';
import { CertificationService } from '@core/services/certification.service';
import { LookupService } from '@core/services/lookup.service';
import {
  CERTIFICATION_LIST_FILTERS_KEY,
  CERTIFICATION_LIST_PAGE_KEY,
  CertificationListComponent
} from './certification-list.component';

describe('CertificationListComponent', () => {
  let service: jasmine.SpyObj<CertificationService>;
  let lookup: jasmine.SpyObj<LookupService>;

  const items: Certification[] = [
    { pkid: 2, partnerPkid: 2, title: 'CCNA', partnerName: 'Cisco', coursePkids: [], jobCategoryPkids: [] },
    { pkid: 1, partnerPkid: 1, title: null, partnerName: 'Microsoft', coursePkids: [], jobCategoryPkids: [] }
  ];

  async function setup(queryParams: Record<string, string> = {}): Promise<ComponentFixture<CertificationListComponent>> {
    await TestBed.configureTestingModule({
      imports: [CertificationListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: CertificationService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CertificationListComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<CertificationService>('CertificationService', ['query', 'delete']);
    service.query.and.returnValue(of(items));
    service.delete.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['partners', 'courses', 'jobCategories']);
    lookup.partners.and.returnValue(of([{ pkid: 1, label: 'Microsoft' }, { pkid: 2, label: 'Cisco' }]));
    lookup.courses.and.returnValue(of([{ pkid: 10, label: 'AZ-104 Azure 管理員' }]));
    lookup.jobCategories.and.returnValue(of([{ pkid: 1, label: '系統管理' }]));
  });

  afterEach(() => sessionStorage.clear());

  it('creates, loads the three filter lookups and queries the service on init', async () => {
    const fixture = await setup();

    expect(fixture.componentInstance).toBeTruthy();
    expect(service.query).toHaveBeenCalledTimes(1);
    expect(lookup.partners).toHaveBeenCalled();
    expect(lookup.courses).toHaveBeenCalled();
    expect(lookup.jobCategories).toHaveBeenCalled();
  });

  it('renders rows with the JOINed partner name and a dash for a null title', async () => {
    const fixture = await setup();
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');

    expect(rows.length).toBe(2);
    const first = rows[0].textContent ?? '';
    expect(first).toContain('2');
    expect(first).toContain('Cisco');
    expect(first).toContain('CCNA');

    const second = rows[1].textContent ?? '';
    expect(second).toContain('Microsoft');
    expect(second).toContain('—');
  });

  it('renders the column headers in the requested order', async () => {
    const fixture = await setup();
    const headers = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('thead th'))
      .map(th => th.textContent?.trim() ?? '');

    expect(headers).toEqual(['主代碼', '原廠', '認證名稱', '操作']);
  });

  it('defaults to sorting by pkid descending', async () => {
    const fixture = await setup();
    const component = fixture.componentInstance as unknown as { sortField: string; sortOrder: number };

    expect(component.sortField).toBe('pkid');
    expect(component.sortOrder).toBe(-1);
  });

  it('applies an incoming partnerPkid query param over the saved filter', async () => {
    sessionStorage.setItem(CERTIFICATION_LIST_FILTERS_KEY, JSON.stringify({ keyword: 'Azure', partnerPkid: 9 }));

    await setup({ partnerPkid: '2' });

    const sent = service.query.calls.mostRecent().args[0];
    expect(sent.partnerPkid).toBe(2);
    expect(sent.keyword).toBe('Azure');
  });

  it('counts active filters for the 搜尋條件 badge', async () => {
    const fixture = await setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null; partnerPkid: number | null; coursePkid: number | null; jobCategoryPkid: number | null };
      activeFilterCount: number;
    };

    expect(component.activeFilterCount).toBe(0);

    component.filters.keyword = 'Azure';
    component.filters.coursePkid = 10;
    component.filters.jobCategoryPkid = 1;
    fixture.detectChanges();

    expect(component.activeFilterCount).toBe(3);
    const filterButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(b => b.textContent?.includes('搜尋條件'))!;
    expect(filterButton.querySelector('.p-badge')?.textContent?.trim()).toBe('3');
  });

  it('persists filters and resets paging on search', async () => {
    const fixture = await setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null; partnerPkid: number | null };
      first: number;
      search: () => void;
    };
    component.first = 40;
    component.filters.keyword = 'CCNA';
    component.filters.partnerPkid = 2;

    component.search();

    expect(component.first).toBe(0);
    const saved = JSON.parse(sessionStorage.getItem(CERTIFICATION_LIST_FILTERS_KEY)!);
    expect(saved.keyword).toBe('CCNA');
    expect(saved.partnerPkid).toBe(2);
    expect(JSON.parse(sessionStorage.getItem(CERTIFICATION_LIST_PAGE_KEY)!).first).toBe(0);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('asks for confirmation with pkid and title before deleting', async () => {
    const fixture = await setup();
    const confirmation = TestBed.inject(ConfirmationService);
    const confirmSpy = spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>2</b>');
      expect(options.message).toContain('「CCNA」');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: (i: Certification) => void }).confirmDelete(items[0]);

    expect(confirmSpy).toHaveBeenCalled();
    expect(service.delete).toHaveBeenCalledWith(2);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('uses the untitled placeholder in the delete confirmation when title is null', async () => {
    const fixture = await setup();
    const confirmation = TestBed.inject(ConfirmationService);
    spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>1</b>');
      expect(options.message).toContain('「(無名稱)」');
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: (i: Certification) => void }).confirmDelete(items[1]);

    expect(service.delete).not.toHaveBeenCalled();
  });
});
