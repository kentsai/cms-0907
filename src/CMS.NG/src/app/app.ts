import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { PanelMenu } from 'primeng/panelmenu';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, PanelMenu, ButtonModule],
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
        }
      ]
    }
  ];

  protected toggleSidebar(): void {
    this.sidebarCollapsed.update(collapsed => !collapsed);
  }
}
