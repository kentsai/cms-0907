import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TablePageEvent } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { forkJoin } from 'rxjs';
import {
  Certification,
  CertificationQuery,
  EMPTY_CERTIFICATION_QUERY,
  certificationLabel
} from '@core/models/certification.model';
import { LookupItem } from '@core/models/lookup-item.model';
import { CertificationService } from '@core/services/certification.service';
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

export const CERTIFICATION_LIST_FILTERS_KEY = 'certification-list-filters';
export const CERTIFICATION_LIST_SORT_KEY = 'certification-list-sort';
export const CERTIFICATION_LIST_PAGE_KEY = 'certification-list-page';

@Component({
  selector: 'app-certification-list',
  imports: [FormsModule, RouterLink, TableModule, ButtonModule, DrawerModule, InputTextModule, SelectModule, TooltipModule],
  templateUrl: './certification-list.component.html',
  styleUrl: './certification-list.component.scss'
})
export class CertificationListComponent implements OnInit {
  private readonly service = inject(CertificationService);
  private readonly lookup = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly items = signal<Certification[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  protected readonly partners = signal<LookupItem[]>([]);
  protected readonly courses = signal<LookupItem[]>([]);
  protected readonly jobCategories = signal<LookupItem[]>([]);

  /** Bound to the filter drawer controls; only applied to the query on 查詢. */
  protected filters: CertificationQuery = { ...EMPTY_CERTIFICATION_QUERY };

  protected sortField = 'pkid';
  protected sortOrder = -1;
  protected first = 0;
  protected rows = 20;
  protected readonly rowsPerPageOptions = [10, 20, 50];

  protected readonly label = certificationLabel;

  ngOnInit(): void {
    this.filters = { ...EMPTY_CERTIFICATION_QUERY, ...readSession<CertificationQuery>(CERTIFICATION_LIST_FILTERS_KEY, {}) };

    // Cross-entity navigation (Partner 查看認證 buttons) overrides the saved filter of the same name.
    const incomingPartner = this.route.snapshot.queryParamMap.get('partnerPkid');
    if (incomingPartner !== null && incomingPartner !== '') {
      this.filters.partnerPkid = Number(incomingPartner);
    }

    const sort = readSession<SortState>(CERTIFICATION_LIST_SORT_KEY, { sortField: 'pkid', sortOrder: -1 });
    this.sortField = sort.sortField;
    this.sortOrder = sort.sortOrder;

    const page = readSession<PageState>(CERTIFICATION_LIST_PAGE_KEY, { first: 0, rows: 20 });
    this.first = page.first;
    this.rows = page.rows;

    // partnerName comes with each row, so the list loads independently of the filter dropdown options.
    forkJoin({
      partners: this.lookup.partners(),
      courses: this.lookup.courses(),
      jobCategories: this.lookup.jobCategories()
    }).subscribe({
      next: ({ partners, courses, jobCategories }) => {
        this.partners.set(partners);
        this.courses.set(courses);
        this.jobCategories.set(jobCategories);
      },
      error: () => this.messages.add({ severity: 'warn', summary: '載入失敗', detail: '無法取得搜尋條件的選項。' })
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
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得認證資料。' });
      }
    });
  }

  protected openFilters(): void {
    this.filterVisible.set(true);
  }

  protected search(): void {
    writeSession(CERTIFICATION_LIST_FILTERS_KEY, this.filters);
    this.first = 0;
    writeSession(CERTIFICATION_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
    this.filterVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.filters = { ...EMPTY_CERTIFICATION_QUERY };
    this.search();
  }

  /** Number of filters currently applied to the query; shown as a badge on the 搜尋條件 button. */
  protected get activeFilterCount(): number {
    const f = this.filters;
    return [
      !!f.keyword?.trim(),
      f.partnerPkid !== null && f.partnerPkid !== undefined,
      f.coursePkid !== null && f.coursePkid !== undefined,
      f.jobCategoryPkid !== null && f.jobCategoryPkid !== undefined
    ].filter(Boolean).length;
  }

  /** Badge text for the 搜尋條件 button; undefined hides the badge when nothing is filtered. */
  protected get filterBadge(): string | undefined {
    const count = this.activeFilterCount;
    return count > 0 ? `${count}` : undefined;
  }

  protected onSort(event: { field?: string; order?: number }): void {
    this.sortField = event.field ?? 'pkid';
    this.sortOrder = event.order ?? -1;
    writeSession(CERTIFICATION_LIST_SORT_KEY, { sortField: this.sortField, sortOrder: this.sortOrder });
  }

  protected onPage(event: TablePageEvent): void {
    this.first = event.first;
    this.rows = event.rows;
    writeSession(CERTIFICATION_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
  }

  protected view(item: Certification): void {
    this.router.navigate(['/course/certifications', item.pkid]);
  }

  protected edit(item: Certification): void {
    this.router.navigate(['/course/certifications', item.pkid, 'edit']);
  }

  protected confirmDelete(item: Certification): void {
    this.confirmation.confirm({
      header: '刪除認證',
      icon: 'pi pi-exclamation-triangle',
      message: `確定要刪除主代碼 <b>${item.pkid}</b>「${certificationLabel(item)}」？`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.delete(item)
    });
  }

  private delete(item: Certification): void {
    this.service.delete(item.pkid).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `主代碼 ${item.pkid}「${certificationLabel(item)}」已刪除。` });
        this.load();
      },
      error: err => {
        const detail = err?.status === 409
          ? (err.error?.message ?? '此認證仍被其他資料使用，無法刪除。')
          : '刪除失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }
}
