import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { PanelMenu } from 'primeng/panelmenu';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { AuthService } from '@core/services/auth.service';

/** Sidebar group that only users with the `Admin` role may see. */
export const ADMIN_MENU_LABEL = '系統管理 Admin';

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

  protected readonly title = signal('CMS-React');
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
