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
        }
      ]
    }
  ];

  protected toggleSidebar(): void {
    this.sidebarCollapsed.update(collapsed => !collapsed);
  }
}
