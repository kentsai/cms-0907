import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule, TablePageEvent } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { EMPTY_PARTNER_QUERY, Partner, PartnerQuery } from '@core/models/partner.model';
import { PartnerService } from '@core/services/partner.service';
import { readSession, writeSession } from '@core/utils/session-storage.util';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

export const PARTNER_LIST_FILTERS_KEY = 'partner-list-filters';
export const PARTNER_LIST_SORT_KEY = 'partner-list-sort';
export const PARTNER_LIST_PAGE_KEY = 'partner-list-page';

@Component({
  selector: 'app-partner-list',
  imports: [FormsModule, RouterLink, TableModule, ButtonModule, DrawerModule, InputTextModule, TooltipModule],
  templateUrl: './partner-list.component.html',
  styleUrl: './partner-list.component.scss'
})
export class PartnerListComponent implements OnInit {
  private readonly service = inject(PartnerService);
  private readonly router = inject(Router);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly items = signal<Partner[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  /** Bound to the filter drawer controls; only applied to the query on 查詢. */
  protected filters: PartnerQuery = { ...EMPTY_PARTNER_QUERY };

  protected sortField = 'displayOrder';
  protected sortOrder = 1;
  protected first = 0;
  protected rows = 20;
  protected readonly rowsPerPageOptions = [10, 20, 50];

  ngOnInit(): void {
    this.filters = { ...EMPTY_PARTNER_QUERY, ...readSession<PartnerQuery>(PARTNER_LIST_FILTERS_KEY, {}) };

    const sort = readSession<SortState>(PARTNER_LIST_SORT_KEY, { sortField: 'displayOrder', sortOrder: 1 });
    this.sortField = sort.sortField;
    this.sortOrder = sort.sortOrder;

    const page = readSession<PageState>(PARTNER_LIST_PAGE_KEY, { first: 0, rows: 20 });
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
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得合作夥伴資料。' });
      }
    });
  }

  protected openFilters(): void {
    this.filterVisible.set(true);
  }

  protected search(): void {
    writeSession(PARTNER_LIST_FILTERS_KEY, this.filters);
    this.first = 0;
    writeSession(PARTNER_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
    this.filterVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.filters = { ...EMPTY_PARTNER_QUERY };
    this.search();
  }

  /** Number of filters currently applied to the query; shown as a badge on the 搜尋條件 button. */
  protected get activeFilterCount(): number {
    return [!!this.filters.keyword?.trim()].filter(Boolean).length;
  }

  /** Badge text for the 搜尋條件 button; undefined hides the badge when nothing is filtered. */
  protected get filterBadge(): string | undefined {
    const count = this.activeFilterCount;
    return count > 0 ? `${count}` : undefined;
  }

  protected onSort(event: { field?: string; order?: number }): void {
    this.sortField = event.field ?? 'displayOrder';
    this.sortOrder = event.order ?? 1;
    writeSession(PARTNER_LIST_SORT_KEY, { sortField: this.sortField, sortOrder: this.sortOrder });
  }

  protected onPage(event: TablePageEvent): void {
    this.first = event.first;
    this.rows = event.rows;
    writeSession(PARTNER_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
  }

  protected view(item: Partner): void {
    this.router.navigate(['/course/partners', item.pkid]);
  }

  protected edit(item: Partner): void {
    this.router.navigate(['/course/partners', item.pkid, 'edit']);
  }

  protected confirmDelete(item: Partner): void {
    this.confirmation.confirm({
      header: '刪除合作夥伴',
      icon: 'pi pi-exclamation-triangle',
      message: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.name}」？`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.delete(item)
    });
  }

  private delete(item: Partner): void {
    this.service.delete(item.pkid).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `主代碼 ${item.pkid}「${item.name}」已刪除。` });
        this.load();
      },
      error: err => {
        const detail = err?.status === 409
          ? (err.error?.message ?? '此合作夥伴仍被其他資料使用，無法刪除。')
          : '刪除失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }
}
