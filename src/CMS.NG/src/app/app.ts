import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { PanelMenu } from 'primeng/panelmenu';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, PanelMenu, ButtonModule, ToastModule, ConfirmDialogModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly title = signal('CMS');
  protected readonly sidebarCollapsed = signal(false);

  protected readonly menuItems: MenuItem[] = [
    {
      label: '系統管理 Admin',
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
        }
      ]
    }
  ];

  protected toggleSidebar(): void {
    this.sidebarCollapsed.update(collapsed => !collapsed);
  }
}
