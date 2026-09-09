import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { MenuItem } from 'primeng/api';
import { PanelMenu } from 'primeng/panelmenu';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { AuthService } from '@core/services/auth.service';

/** Sidebar group that only users with the `Admin` role may see. */
export const ADMIN_MENU_LABEL = '系統管理 Admin';

/**
 * Route `data` key. A route declared with `data: { chromeless: true }` (e.g. the course print view) is rendered
 * with nothing but the outlet: no topbar, sidebar, toast or confirm dialog, and no scrolling shell grid around it.
 */
export const CHROMELESS_ROUTE_DATA = 'chromeless';

/** Whether the deepest activated route carries `data.chromeless === true`. */
export function isChromelessRoute(router: Router): boolean {
  let route = router.routerState.root;
  while (route.firstChild) {
    route = route.firstChild;
  }
  return route.snapshot.data[CHROMELESS_ROUTE_DATA] === true;
}

const MENU_ITEMS: MenuItem[] = [
  {
    label: '首頁 Home',
    icon: 'pi pi-home',
    expanded: true,
    items: [
      {
        label: '上稿作業 FeaturedPromoItem',
        icon: 'pi pi-calendar',
        routerLink: '/home/featured-promo-items'
      }
    ]
  },
  {
    label: ADMIN_MENU_LABEL,
    icon: 'pi pi-cog',
    expanded: true,
    items: [
      {
        label: '角色 AppRole',
        icon: 'pi pi-users',
        routerLink: '/admin/app-roles'
      },
      {
        label: '使用者 AppUser',
        icon: 'pi pi-user',
        routerLink: '/admin/app-users'
      },
      {
        label: '發布狀態 PublishStatus',
        icon: 'pi pi-flag',
        routerLink: '/admin/publish-statuses'
      }
    ]
  },
  {
    label: '課程管理 Course',
    icon: 'pi pi-book',
    expanded: true,
    items: [
      {
        label: '合作夥伴 Partner',
        icon: 'pi pi-building',
        routerLink: '/course/partners'
      },
      {
        label: '課程群組 CourseGroup',
        icon: 'pi pi-folder',
        routerLink: '/course/course-groups'
      },
      {
        label: '課程 Course',
        icon: 'pi pi-graduation-cap',
        routerLink: '/course/courses'
      },
      {
        label: '認證 Certification',
        icon: 'pi pi-verified',
        routerLink: '/course/certifications'
      }
    ]
  }
];

/**
 * App shell: topbar (signed-in user + logout) + collapsible sidebar + router outlet. The chrome is
 * only rendered while signed in, so the login page gets the full viewport. While the session still has to
 * change its default password (`AuthService.mustChangePassword`) the sidebar and the profile link are
 * withheld as well — the change-password page is the only destination.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, PanelMenu, ButtonModule, ToastModule, ConfirmDialogModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly title = signal('CMS-React');
  /** True while the active leaf route is chromeless (see {@link CHROMELESS_ROUTE_DATA}); re-evaluated on every navigation. */
  protected readonly chromeless = toSignal(
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      map(() => isChromelessRoute(this.router))
    ),
    { initialValue: isChromelessRoute(this.router) }
  );
  /** Starts collapsed on narrow viewports (phones), where the sidebar is an overlay rather than a column. */
  protected readonly sidebarCollapsed = signal(
    typeof window !== 'undefined' && window.matchMedia?.('(max-width: 640px)').matches === true
  );

  /** The full menu for Admins; everyone else gets it without the `系統管理 Admin` group. */
  protected readonly menuItems = computed<MenuItem[]>(() =>
    this.auth.isAdmin() ? MENU_ITEMS : MENU_ITEMS.filter(item => item.label !== ADMIN_MENU_LABEL)
  );

  protected toggleSidebar(): void {
    this.sidebarCollapsed.update(collapsed => !collapsed);
  }

  protected logout(): void {
    this.auth.logout();
  }
}
