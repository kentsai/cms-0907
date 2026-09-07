import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { AppRole } from '@core/models/app-role.model';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { APP_ROLE_LIST_FILTERS_KEY, APP_ROLE_LIST_PAGE_KEY, AppRoleListComponent } from './app-role-list.component';

type ListInternals = {
  filters: { keyword: string | null; permissionLevel: number | null; userId: string | null };
  first: number;
  activeFilterCount: number;
  filterVisible: () => boolean;
  search: () => void;
  confirmDelete: (item: AppRole) => void;
};

describe('AppRoleListComponent', () => {
  let service: jasmine.SpyObj<AppRoleService>;
  let lookup: jasmine.SpyObj<LookupService>;

  const items: AppRole[] = [
    { pkid: 1, roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1, description: '系統管理員', userCount: 3, userIds: [] },
    { pkid: 2, roleId: 'User', roleName: 'User', permissionLevel: 100, description: '一般使用者', userCount: 0, userIds: [] }
  ];

  async function setup(queryParams: Record<string, string> = {}): Promise<ComponentFixture<AppRoleListComponent>> {
    await TestBed.configureTestingModule({
      imports: [AppRoleListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: AppRoleService, useValue: service },
        { provide: LookupService, useValue: lookup },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AppRoleListComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['query', 'delete']);
    service.query.and.returnValue(of(items));
    service.delete.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['appUsers']);
    lookup.appUsers.and.returnValue(of([{ id: 'helen', label: 'helen (helen)' }]));
  });

  afterEach(() => sessionStorage.clear());

  it('loads the user lookup then queries the service', async () => {
    await setup();
    expect(lookup.appUsers).toHaveBeenCalledTimes(1);
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('renders one row per role with the sample columns', async () => {
    const fixture = await setup();
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');

    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Admin');
    expect(rows[0].textContent).toContain('Administrator');
    expect(rows[0].textContent).toContain('系統管理員');
    expect(rows[0].textContent).toContain('3');
  });

  it('opens the filter drawer from the 搜尋條件 button', async () => {
    const fixture = await setup();
    const c = fixture.componentInstance as unknown as ListInternals;
    expect(c.filterVisible()).toBeFalse();

    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(b => b.textContent?.includes('搜尋條件'))!
      .click();
    fixture.detectChanges();

    expect(c.filterVisible()).toBeTrue();
  });

  it('shows the active filter count as a badge', async () => {
    const fixture = await setup();
    const c = fixture.componentInstance as unknown as ListInternals;
    const filterButton = () =>
      Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
        .find(b => b.textContent?.includes('搜尋條件'))!;

    expect(c.activeFilterCount).toBe(0);
    expect(filterButton().querySelector('.p-badge')).toBeNull();

    c.filters.keyword = 'Adm';
    c.filters.permissionLevel = 1;
    fixture.detectChanges();

    expect(filterButton().querySelector('.p-badge')?.textContent?.trim()).toBe('2');
  });

  it('persists filters and resets paging on search', async () => {
    const fixture = await setup();
    const c = fixture.componentInstance as unknown as ListInternals;
    c.first = 20;
    c.filters.keyword = 'Adm';

    c.search();

    expect(c.first).toBe(0);
    expect(JSON.parse(sessionStorage.getItem(APP_ROLE_LIST_FILTERS_KEY)!).keyword).toBe('Adm');
    expect(JSON.parse(sessionStorage.getItem(APP_ROLE_LIST_PAGE_KEY)!).first).toBe(0);
    expect(service.query.calls.mostRecent().args[0].keyword).toBe('Adm');
  });

  it('lets an incoming userId query param override the saved filter', async () => {
    sessionStorage.setItem(APP_ROLE_LIST_FILTERS_KEY, JSON.stringify({ keyword: 'x', userId: 'someone-else' }));

    await setup({ userId: 'helen' });

    const args = service.query.calls.mostRecent().args[0];
    expect(args.userId).toBe('helen');
    expect(args.keyword).toBe('x');
  });

  it('confirms with pkid, roleName and the user-count warning before deleting by roleId', async () => {
    const fixture = await setup();
    const confirmation = TestBed.inject(ConfirmationService);
    spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>1</b>');
      expect(options.message).toContain('「Administrator」');
      expect(options.message).toContain('3 位使用者');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as ListInternals).confirmDelete(items[0]);

    expect(service.delete).toHaveBeenCalledWith('Admin');
    expect(service.query).toHaveBeenCalledTimes(2);
  });
});
