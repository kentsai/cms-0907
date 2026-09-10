import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { AppUser } from '@core/models/app-user.model';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import {
  APP_USER_LIST_FILTERS_KEY,
  APP_USER_LIST_PAGE_KEY,
  AppUserListComponent
} from './app-user-list.component';

describe('AppUserListComponent', () => {
  let service: jasmine.SpyObj<AppUserService>;
  let lookup: jasmine.SpyObj<LookupService>;

  const items: AppUser[] = [
    { pkid: 1, userId: 'helen', userName: 'Helen Chen', isActive: true, passwordUpdatedTime: '2026-09-01T08:00:00', roleCount: 2, roleIds: [] },
    { pkid: 2, userId: 'miles', userName: 'Miles Sun', isActive: false, passwordUpdatedTime: null, roleCount: 0, roleIds: [] }
  ];

  async function setup(queryParams: Record<string, string> = {}): Promise<ComponentFixture<AppUserListComponent>> {
    await TestBed.configureTestingModule({
      imports: [AppUserListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: AppUserService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AppUserListComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<AppUserService>('AppUserService', ['query', 'delete']);
    service.query.and.returnValue(of(items));
    service.delete.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['appRoles']);
    lookup.appRoles.and.returnValue(of([{ id: 'Admin', label: 'Administrator' }, { id: 'Editor', label: 'Editor' }]));
  });

  afterEach(() => sessionStorage.clear());

  it('creates, loads the role lookup and queries the service on init', async () => {
    const fixture = await setup();

    expect(fixture.componentInstance).toBeTruthy();
    expect(lookup.appRoles).toHaveBeenCalled();
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('renders rows with the active tag, password time and role count, and no password column', async () => {
    const fixture = await setup();
    const host = fixture.nativeElement as HTMLElement;
    const rows = host.querySelectorAll('tbody tr');

    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('helen');
    expect(rows[0].textContent).toContain('Helen Chen');
    expect(rows[0].textContent).toContain('啟用');
    expect(rows[0].textContent).toContain('2026-09-01');
    expect(rows[0].textContent).toContain('查看角色');
    expect(rows[1].textContent).toContain('停用');
    expect(rows[1].textContent).toContain('—');

    const headers = Array.from(host.querySelectorAll('thead th')).map(th => th.textContent ?? '');
    expect(headers.some(h => h.includes('密碼雜湊') || h.toLowerCase().includes('hash'))).toBeFalse();
  });

  it('applies an incoming roleId query param over the saved filter', async () => {
    sessionStorage.setItem(APP_USER_LIST_FILTERS_KEY, JSON.stringify({ keyword: 'hel', roleId: 'Editor' }));

    await setup({ roleId: 'Admin' });

    const sent = service.query.calls.mostRecent().args[0];
    expect(sent.roleId).toBe('Admin');
    expect(sent.keyword).toBe('hel');
  });

  it('counts active filters for the 搜尋條件 badge', async () => {
    const fixture = await setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null; isActive: boolean | null; roleId: string | null };
      activeFilterCount: number;
    };

    expect(component.activeFilterCount).toBe(0);

    component.filters.keyword = 'hel';
    component.filters.isActive = false;
    component.filters.roleId = 'Admin';
    fixture.detectChanges();

    expect(component.activeFilterCount).toBe(3);
    const filterButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(b => b.textContent?.includes('搜尋條件'))!;
    expect(filterButton.querySelector('.p-badge')?.textContent?.trim()).toBe('3');
  });

  it('persists filters and resets paging to the first page on search', async () => {
    const fixture = await setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null };
      first: number;
      search: () => void;
    };
    component.first = 40;
    component.filters.keyword = 'mil';

    component.search();

    expect(component.first).toBe(0);
    expect(JSON.parse(sessionStorage.getItem(APP_USER_LIST_FILTERS_KEY)!).keyword).toBe('mil');
    expect(JSON.parse(sessionStorage.getItem(APP_USER_LIST_PAGE_KEY)!).first).toBe(0);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('asks for confirmation with pkid, name and role warning before deleting', async () => {
    const fixture = await setup();
    const confirmation = TestBed.inject(ConfirmationService);
    spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>1</b>');
      expect(options.message).toContain('「Helen Chen」');
      expect(options.message).toContain('2 個角色');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: (i: AppUser) => void }).confirmDelete(items[0]);

    expect(service.delete).toHaveBeenCalledWith('helen');
    expect(service.query).toHaveBeenCalledTimes(2);
  });
});
