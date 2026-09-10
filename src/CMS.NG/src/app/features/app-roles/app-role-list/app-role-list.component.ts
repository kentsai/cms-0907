import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TablePageEvent } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { forkJoin } from 'rxjs';
import { AppRole, AppRoleQuery, EMPTY_APP_ROLE_QUERY } from '@core/models/app-role.model';
import { StringLookupItem } from '@core/models/lookup-item.model';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { readSession, writeSession } from '@core/utils/session-storage.util';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

export const APP_ROLE_LIST_FILTERS_KEY = 'app-role-list-filters';
export const APP_ROLE_LIST_SORT_KEY = 'app-role-list-sort';
export const APP_ROLE_LIST_PAGE_KEY = 'app-role-list-page';

@Component({
  selector: 'app-app-role-list',
  imports: [FormsModule, RouterLink, TableModule, ButtonModule, DrawerModule, InputNumberModule, InputTextModule, SelectModule, TooltipModule],
  templateUrl: './app-role-list.component.html',
  styleUrl: './app-role-list.component.scss'
})
export class AppRoleListComponent implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly lookup = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly items = signal<AppRole[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  /** User options for the 使用者 filter (`全部` is added in the template via showClear). */
  protected readonly users = signal<StringLookupItem[]>([]);

  /** Bound to the filter drawer controls; only applied to the query on 查詢. */
  protected filters: AppRoleQuery = { ...EMPTY_APP_ROLE_QUERY };

  protected sortField = 'roleId';
  protected sortOrder = 1;
  protected first = 0;
  protected rows = 20;
  protected readonly rowsPerPageOptions = [10, 20, 50];

  ngOnInit(): void {
    this.filters = { ...EMPTY_APP_ROLE_QUERY, ...readSession<AppRoleQuery>(APP_ROLE_LIST_FILTERS_KEY, {}) };

    // Cross-entity navigation (e.g. from a future AppUser page) overrides the saved filter.
    const incomingUserId = this.route.snapshot.queryParamMap.get('userId');
    if (incomingUserId) {
      this.filters.userId = incomingUserId;
      writeSession(APP_ROLE_LIST_FILTERS_KEY, this.filters);
    }

    const sort = readSession<SortState>(APP_ROLE_LIST_SORT_KEY, { sortField: 'roleId', sortOrder: 1 });
    this.sortField = sort.sortField;
    this.sortOrder = sort.sortOrder;

    const page = readSession<PageState>(APP_ROLE_LIST_PAGE_KEY, { first: 0, rows: 20 });
    this.first = page.first;
    this.rows = page.rows;

    this.loading.set(true);
    forkJoin({ users: this.lookup.appUsers() }).subscribe({
      next: ({ users }) => {
        this.users.set(users);
        this.load();
      },
      error: () => {
        // The list is still useful without the user filter options.
        this.messages.add({ severity: 'warn', summary: '使用者清單載入失敗', detail: '使用者篩選選項暫時無法使用。' });
        this.load();
      }
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.filters).subscribe({
      next: items => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得角色資料。' });
      }
    });
  }

  protected openFilters(): void {
    this.filterVisible.set(true);
  }

  protected search(): void {
    writeSession(APP_ROLE_LIST_FILTERS_KEY, this.filters);
    this.first = 0;
    writeSession(APP_ROLE_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
    this.filterVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.filters = { ...EMPTY_APP_ROLE_QUERY };
    this.search();
  }

  /** Number of filters currently applied; shown as a badge on the 搜尋條件 button. */
  protected get activeFilterCount(): number {
    const f = this.filters;
    return [!!f.keyword?.trim(), f.permissionLevel != null, !!f.userId].filter(Boolean).length;
  }

  protected get filterBadge(): string | undefined {
    const count = this.activeFilterCount;
    return count > 0 ? `${count}` : undefined;
  }

  protected onSort(event: { field?: string; order?: number }): void {
    this.sortField = event.field ?? 'roleId';
    this.sortOrder = event.order ?? 1;
    writeSession(APP_ROLE_LIST_SORT_KEY, { sortField: this.sortField, sortOrder: this.sortOrder });
  }

  protected onPage(event: TablePageEvent): void {
    this.first = event.first;
    this.rows = event.rows;
    writeSession(APP_ROLE_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
  }

  protected view(item: AppRole): void {
    this.router.navigate(['/admin/app-roles', item.roleId]);
  }

  protected edit(item: AppRole): void {
    this.router.navigate(['/admin/app-roles', item.roleId, 'edit']);
  }

  protected confirmDelete(item: AppRole): void {
    const warning = item.userCount > 0 ? `<br/>（將同時移除 ${item.userCount} 位使用者的此角色）` : '';
    this.confirmation.confirm({
      header: '刪除角色',
      icon: 'pi pi-exclamation-triangle',
      message: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.roleName}」？${warning}`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.delete(item)
    });
  }

  private delete(item: AppRole): void {
    this.service.delete(item.roleId).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `角色「${item.roleName}」已刪除。` });
        this.load();
      },
      error: err => {
        const detail = err?.status === 409
          ? (err.error?.message ?? '此角色仍被其他資料使用，無法刪除。')
          : '刪除失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }
}
