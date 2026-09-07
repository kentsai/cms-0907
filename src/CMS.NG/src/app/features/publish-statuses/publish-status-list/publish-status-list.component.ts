import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TablePageEvent } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { EMPTY_PUBLISH_STATUS_QUERY, PublishStatus, PublishStatusQuery } from '@core/models/publish-status.model';
import { PublishStatusService } from '@core/services/publish-status.service';
import { readSession, writeSession } from '@core/utils/session-storage.util';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

export const PUBLISH_STATUS_LIST_FILTERS_KEY = 'publish-status-list-filters';
export const PUBLISH_STATUS_LIST_SORT_KEY = 'publish-status-list-sort';
export const PUBLISH_STATUS_LIST_PAGE_KEY = 'publish-status-list-page';

@Component({
  selector: 'app-publish-status-list',
  imports: [FormsModule, RouterLink, TableModule, ButtonModule, DrawerModule, InputTextModule, SelectModule, TooltipModule],
  templateUrl: './publish-status-list.component.html',
  styleUrl: './publish-status-list.component.scss'
})
export class PublishStatusListComponent implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly router = inject(Router);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly items = signal<PublishStatus[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  /** Bound to the filter drawer controls; only applied to the query on 查詢. */
  protected filters: PublishStatusQuery = { ...EMPTY_PUBLISH_STATUS_QUERY };

  protected sortField = 'pkid';
  protected sortOrder = 1;
  protected first = 0;
  protected rows = 20;
  protected readonly rowsPerPageOptions = [10, 20, 50];

  protected readonly boolOptions: { label: string; value: boolean | null }[] = [
    { label: '全部', value: null },
    { label: '是', value: true },
    { label: '否', value: false }
  ];

  ngOnInit(): void {
    this.filters = { ...EMPTY_PUBLISH_STATUS_QUERY, ...readSession<PublishStatusQuery>(PUBLISH_STATUS_LIST_FILTERS_KEY, {}) };

    const sort = readSession<SortState>(PUBLISH_STATUS_LIST_SORT_KEY, { sortField: 'pkid', sortOrder: 1 });
    this.sortField = sort.sortField;
    this.sortOrder = sort.sortOrder;

    const page = readSession<PageState>(PUBLISH_STATUS_LIST_PAGE_KEY, { first: 0, rows: 20 });
    this.first = page.first;
    this.rows = page.rows;

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
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得發布狀態資料。' });
      }
    });
  }

  protected openFilters(): void {
    this.filterVisible.set(true);
  }

  protected search(): void {
    writeSession(PUBLISH_STATUS_LIST_FILTERS_KEY, this.filters);
    this.first = 0;
    writeSession(PUBLISH_STATUS_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
    this.filterVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.filters = { ...EMPTY_PUBLISH_STATUS_QUERY };
    this.search();
  }

  /** Number of filters currently applied to the query; shown as a badge on the 搜尋條件 button. */
  protected get activeFilterCount(): number {
    const f = this.filters;
    return [
      !!f.keyword?.trim(),
      f.isDraft != null,
      f.isPublished != null,
      f.isDiscontinued != null
    ].filter(Boolean).length;
  }

  /** Badge text for the 搜尋條件 button; undefined hides the badge when nothing is filtered. */
  protected get filterBadge(): string | undefined {
    const count = this.activeFilterCount;
    return count > 0 ? `${count}` : undefined;
  }

  protected onSort(event: { field?: string; order?: number }): void {
    this.sortField = event.field ?? 'pkid';
    this.sortOrder = event.order ?? 1;
    writeSession(PUBLISH_STATUS_LIST_SORT_KEY, { sortField: this.sortField, sortOrder: this.sortOrder });
  }

  protected onPage(event: TablePageEvent): void {
    this.first = event.first;
    this.rows = event.rows;
    writeSession(PUBLISH_STATUS_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
  }

  protected view(item: PublishStatus): void {
    this.router.navigate(['/admin/publish-statuses', item.pkid]);
  }

  protected edit(item: PublishStatus): void {
    this.router.navigate(['/admin/publish-statuses', item.pkid, 'edit']);
  }

  protected confirmDelete(item: PublishStatus): void {
    this.confirmation.confirm({
      header: '刪除發布狀態',
      icon: 'pi pi-exclamation-triangle',
      message: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.delete(item)
    });
  }

  private delete(item: PublishStatus): void {
    this.service.delete(item.pkid).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `主代碼 ${item.pkid}「${item.description}」已刪除。` });
        this.load();
      },
      error: err => {
        const detail = err?.status === 409
          ? (err.error?.message ?? '此發布狀態仍被其他資料使用，無法刪除。')
          : '刪除失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }
}
