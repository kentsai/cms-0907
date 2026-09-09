import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, Routes, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { AuthService, LOGIN_PATH } from '@core/services/auth.service';
import { seedSignedInUser } from '@app/testing/auth-testing';
import { ADMIN_MENU_LABEL, App } from './app';

describe('App', () => {
  /** Mounts the shell; the profile must be in session storage BEFORE this runs (AuthService reads it on creation). */
  async function mount(): Promise<ComponentFixture<App>> {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        MessageService,
        ConfirmationService
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture;
  }

  const sidebarText = (fixture: ComponentFixture<App>) =>
    (fixture.nativeElement as HTMLElement).querySelector('.app-sidebar')?.textContent ?? '';

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  describe('signed in as Admin', () => {
    beforeEach(() => seedSignedInUser(['Admin'], '陳小美'));

    it('should create the app', async () => {
      const fixture = await mount();
      expect(fixture.componentInstance).toBeTruthy();
    });

    it('should render the application title in the topbar', async () => {
      const fixture = await mount();

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('.app-topbar__title')?.textContent).toContain('CMS-React');
    });

    it('should show the signed-in UserName in the topbar', async () => {
      const fixture = await mount();

      const user = (fixture.nativeElement as HTMLElement).querySelector('.app-topbar__user');
      expect(user?.textContent).toContain('陳小美');
    });

    it('should render the Admin / AppRole sidebar menu', async () => {
      const fixture = await mount();
      expect(sidebarText(fixture)).toContain(ADMIN_MENU_LABEL);
      expect(sidebarText(fixture)).toContain('角色 AppRole');
    });

    it('should list AppUser under the Admin menu group', async () => {
      const fixture = await mount();
      expect(sidebarText(fixture)).toContain('使用者 AppUser');
    });

    it('should list PublishStatus under the Admin menu group', async () => {
      const fixture = await mount();
      expect(sidebarText(fixture)).toContain('發布狀態 PublishStatus');
    });

    it('should list Partner under the Course menu group', async () => {
      const fixture = await mount();
      expect(sidebarText(fixture)).toContain('課程管理 Course');
      expect(sidebarText(fixture)).toContain('合作夥伴 Partner');
    });

    it('should list CourseGroup under the Course menu group', async () => {
      const fixture = await mount();
      expect(sidebarText(fixture)).toContain('課程群組 CourseGroup');
    });

    it('should list Course under the Course menu group', async () => {
      const fixture = await mount();
      expect(sidebarText(fixture)).toContain('課程 Course');
    });

    it('should list FeaturedPromoItem under the Home menu group', async () => {
      const fixture = await mount();
      expect(sidebarText(fixture)).toContain('首頁 Home');
      expect(sidebarText(fixture)).toContain('上稿作業 FeaturedPromoItem');
    });

    it('should list Certification under the Course menu group', async () => {
      const fixture = await mount();
      expect(sidebarText(fixture)).toContain('認證 Certification');
    });

    it('should toggle the sidebar collapsed state', async () => {
      const fixture = await mount();

      const shell = (fixture.nativeElement as HTMLElement).querySelector('.app-shell')!;
      expect(shell.classList).not.toContain('app-shell--collapsed');

      (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.app-topbar__toggle')!.click();
      fixture.detectChanges();

      expect(shell.classList).toContain('app-shell--collapsed');
    });

    it('links the user name to the My Profile page', async () => {
      const fixture = await mount();

      const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('a.app-topbar__user')!;
      expect(link.getAttribute('href')).toBe('/profile');
      expect(link.textContent).toContain('個人資料');
    });

    it('refreshes the topbar name after the profile is saved', async () => {
      const fixture = await mount();
      const backend = TestBed.inject(HttpTestingController);

      TestBed.inject(AuthService).updateProfile('陳大美').subscribe();
      backend.expectOne(`${environment.apiBaseUrl}/auth/profile`).flush({ userId: 'mei', userName: '陳大美', roles: ['Admin'] });
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).querySelector('.app-topbar__user')?.textContent).toContain('陳大美');
      expect((fixture.nativeElement as HTMLElement).querySelector('.app-topbar__user')?.textContent).not.toContain('陳小美');
      backend.verify();
    });

    it('logout clears session storage and returns to the login page', async () => {
      const fixture = await mount();
      sessionStorage.setItem('course-list-filters', '{}');
      const navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);

      (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.app-topbar__logout')!.click();
      fixture.detectChanges();

      expect(sessionStorage.length).toBe(0);
      expect(navigate).toHaveBeenCalledWith(LOGIN_PATH);
      expect((fixture.nativeElement as HTMLElement).querySelector('.app-sidebar')).toBeNull();
    });
  });

  describe('signed in without the Admin role', () => {
    beforeEach(() => seedSignedInUser(['Editor'], '王大明'));

    it('hides the Admin menu group but keeps the others', async () => {
      const fixture = await mount();

      const text = sidebarText(fixture);
      expect(text).not.toContain(ADMIN_MENU_LABEL);
      expect(text).not.toContain('使用者 AppUser');
      expect(text).toContain('首頁 Home');
      expect(text).toContain('課程管理 Course');
    });

    it('still shows the user name and logout', async () => {
      const fixture = await mount();

      const el = fixture.nativeElement as HTMLElement;
      expect(el.querySelector('.app-topbar__user')?.textContent).toContain('王大明');
      expect(el.querySelector('.app-topbar__logout')).not.toBeNull();
    });
  });

  describe('signed in with several roles', () => {
    it('shows the Admin menu group when Admin is one of them', async () => {
      seedSignedInUser(['Editor', 'Admin']);

      const fixture = await mount();

      expect(sidebarText(fixture)).toContain(ADMIN_MENU_LABEL);
    });
  });

  describe('signed in with the default password (must change it first)', () => {
    beforeEach(() => seedSignedInUser(['Admin'], '陳小美', true));

    it('renders the topbar without the sidebar, the menu toggle or the profile link', async () => {
      const fixture = await mount();

      const el = fixture.nativeElement as HTMLElement;
      expect(el.querySelector('.app-shell')?.classList).toContain('app-shell--locked');
      expect(el.querySelector('.app-topbar')).not.toBeNull();
      expect(el.querySelector('.app-sidebar')).toBeNull();
      expect(el.querySelector('.app-topbar__toggle')).toBeNull();
      expect(el.querySelector('a.app-topbar__user')).toBeNull();
      expect(el.querySelector('.app-topbar__user')?.textContent).toContain('陳小美');
      expect(el.querySelector('.app-topbar__user')?.textContent).toContain('請先變更密碼');
    });

    it('still offers 登出', async () => {
      const fixture = await mount();

      expect((fixture.nativeElement as HTMLElement).querySelector('.app-topbar__logout')).not.toBeNull();
    });
  });

  describe('signed out', () => {
    it('renders neither topbar nor sidebar, only the outlet', async () => {
      const fixture = await mount();

      const el = fixture.nativeElement as HTMLElement;
      expect(el.querySelector('.app-topbar')).toBeNull();
      expect(el.querySelector('.app-sidebar')).toBeNull();
      expect(el.querySelector('.app-shell')?.classList).toContain('app-shell--anonymous');
      expect(el.querySelector('router-outlet')).not.toBeNull();
    });
  });

  describe('chromeless routes (data.chromeless, e.g. the course print view)', () => {
    @Component({ template: '<p class="stub-page">stub page</p>' })
    class StubPageComponent {}

    const routes: Routes = [
      { path: 'print', component: StubPageComponent, data: { chromeless: true } },
      { path: 'normal', component: StubPageComponent }
    ];

    /** Mounts the shell with real routes so navigation drives the `chromeless` signal. */
    async function mountWithRoutes(): Promise<ComponentFixture<App>> {
      await TestBed.configureTestingModule({
        imports: [App],
        providers: [
          provideRouter(routes),
          provideNoopAnimations(),
          provideHttpClient(),
          provideHttpClientTesting(),
          MessageService,
          ConfirmationService
        ]
      }).compileComponents();
      const fixture = TestBed.createComponent(App);
      fixture.detectChanges();
      return fixture;
    }

    async function go(fixture: ComponentFixture<App>, url: string): Promise<void> {
      await TestBed.inject(Router).navigateByUrl(url);
      fixture.detectChanges();
    }

    beforeEach(() => seedSignedInUser(['Admin'], '陳小美'));

    it('renders only the routed page: no shell, topbar, sidebar, toast or confirm dialog', async () => {
      const fixture = await mountWithRoutes();
      await go(fixture, '/print');

      const el = fixture.nativeElement as HTMLElement;
      expect(el.querySelector('.stub-page')?.textContent).toBe('stub page');
      expect(el.querySelector('.app-shell')).toBeNull();
      expect(el.querySelector('.app-topbar')).toBeNull();
      expect(el.querySelector('.app-sidebar')).toBeNull();
      expect(el.querySelector('p-toast')).toBeNull();
      expect(el.querySelector('p-confirmdialog')).toBeNull();
    });

    it('restores the shell, toast and confirm dialog on a normal route', async () => {
      const fixture = await mountWithRoutes();
      await go(fixture, '/print');
      await go(fixture, '/normal');

      const el = fixture.nativeElement as HTMLElement;
      expect(el.querySelector('.stub-page')).not.toBeNull();
      expect(el.querySelector('.app-shell')).not.toBeNull();
      expect(el.querySelector('.app-topbar')).not.toBeNull();
      expect(el.querySelector('.app-sidebar')).not.toBeNull();
      expect(el.querySelector('p-toast')).not.toBeNull();
      expect(el.querySelector('p-confirmdialog')).not.toBeNull();
    });
  });
});
