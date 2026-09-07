import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TablePageEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { forkJoin } from 'rxjs';
import { AppUser, AppUserQuery, EMPTY_APP_USER_QUERY } from '@core/models/app-user.model';
import { StringLookupItem } from '@core/models/lookup-item.model';
import { AppUserService } from '@core/services/app-user.service';
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

export const APP_USER_LIST_FILTERS_KEY = 'app-user-list-filters';
export const APP_USER_LIST_SORT_KEY = 'app-user-list-sort';
export const APP_USER_LIST_PAGE_KEY = 'app-user-list-page';

@Component({
  selector: 'app-app-user-list',
  imports: [DatePipe, FormsModule, RouterLink, TableModule, ButtonModule, DrawerModule, InputTextModule, SelectModule, TagModule, TooltipModule],
  templateUrl: './app-user-list.component.html',
  styleUrl: './app-user-list.component.scss'
})
export class AppUserListComponent implements OnInit {
  private readonly service = inject(AppUserService);
  private readonly lookup = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly items = signal<AppUser[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  /** Role options for the 角色 filter. */
  protected readonly roles = signal<StringLookupItem[]>([]);

  /** Bound to the filter drawer controls; only applied to the query on 查詢. */
  protected filters: AppUserQuery = { ...EMPTY_APP_USER_QUERY };

  protected readonly boolOptions: { label: string; value: boolean | null }[] = [
    { label: '全部', value: null },
    { label: '是', value: true },
    { label: '否', value: false }
  ];

  protected sortField = 'userId';
  protected sortOrder = 1;
  protected first = 0;
  protected rows = 20;
  protected readonly rowsPerPageOptions = [10, 20, 50];

  ngOnInit(): void {
    this.filters = { ...EMPTY_APP_USER_QUERY, ...readSession<AppUserQuery>(APP_USER_LIST_FILTERS_KEY, {}) };

    // Cross-entity navigation (e.g. from an AppRole page) overrides the saved filter.
    const incomingRoleId = this.route.snapshot.queryParamMap.get('roleId');
    if (incomingRoleId) {
      this.filters.roleId = incomingRoleId;
      writeSession(APP_USER_LIST_FILTERS_KEY, this.filters);
    }

    const sort = readSession<SortState>(APP_USER_LIST_SORT_KEY, { sortField: 'userId', sortOrder: 1 });
    this.sortField = sort.sortField;
    this.sortOrder = sort.sortOrder;

    const page = readSession<PageState>(APP_USER_LIST_PAGE_KEY, { first: 0, rows: 20 });
    this.first = page.first;
    this.rows = page.rows;

    forkJoin({ roles: this.lookup.appRoles() }).subscribe({
      next: ({ roles }) => this.roles.set(roles),
      error: () => this.messages.add({ severity: 'warn', summary: '角色清單載入失敗', detail: '角色篩選選項暫時無法使用。' })
    });

    this.load();
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
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得使用者資料。' });
      }
    });
  }

  protected openFilters(): void {
    this.filterVisible.set(true);
  }

  protected search(): void {
    writeSession(APP_USER_LIST_FILTERS_KEY, this.filters);
    this.first = 0;
    writeSession(APP_USER_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
    this.filterVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.filters = { ...EMPTY_APP_USER_QUERY };
    this.search();
  }

  /** Number of filters currently applied; shown as a badge on the 搜尋條件 button. */
  protected get activeFilterCount(): number {
    const f = this.filters;
    return [!!f.keyword?.trim(), f.isActive !== null && f.isActive !== undefined, !!f.roleId].filter(Boolean).length;
  }

  protected get filterBadge(): string | undefined {
    const count = this.activeFilterCount;
    return count > 0 ? `${count}` : undefined;
  }

  protected onSort(event: { field?: string; order?: number }): void {
    this.sortField = event.field ?? 'userId';
    this.sortOrder = event.order ?? 1;
    writeSession(APP_USER_LIST_SORT_KEY, { sortField: this.sortField, sortOrder: this.sortOrder });
  }

  protected onPage(event: TablePageEvent): void {
    this.first = event.first;
    this.rows = event.rows;
    writeSession(APP_USER_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
  }

  protected view(item: AppUser): void {
    this.router.navigate(['/admin/app-users', item.userId]);
  }

  protected edit(item: AppUser): void {
    this.router.navigate(['/admin/app-users', item.userId, 'edit']);
  }

  protected confirmDelete(item: AppUser): void {
    const warning = item.roleCount > 0 ? `<br/>（將同時移除此使用者的 ${item.roleCount} 個角色）` : '';
    this.confirmation.confirm({
      header: '刪除使用者',
      icon: 'pi pi-exclamation-triangle',
      message: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.userName}」？${warning}`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.delete(item)
    });
  }

  private delete(item: AppUser): void {
    this.service.delete(item.userId).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `使用者「${item.userName}」已刪除。` });
        this.load();
      },
      error: err => {
        const detail = err?.status === 409
          ? (err.error?.message ?? '此使用者仍被其他資料使用，無法刪除。')
          : '刪除失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }
}
