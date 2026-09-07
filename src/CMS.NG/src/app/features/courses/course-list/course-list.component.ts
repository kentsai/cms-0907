import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TablePageEvent } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { forkJoin } from 'rxjs';
import { Course, CourseQuery, EMPTY_COURSE_QUERY } from '@core/models/course.model';
import { LookupItem } from '@core/models/lookup-item.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { fromIso, toIso } from '@core/utils/date.util';
import { readSession, writeSession } from '@core/utils/session-storage.util';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

/** Date-range filter values as `Date` objects for the p-datepicker controls; serialised to ISO on search. */
interface DateFilters {
  scheduleOnFrom: Date | null;
  scheduleOnTo: Date | null;
  scheduleOffFrom: Date | null;
  scheduleOffTo: Date | null;
}

export const COURSE_LIST_FILTERS_KEY = 'course-list-filters';
export const COURSE_LIST_SORT_KEY = 'course-list-sort';
export const COURSE_LIST_PAGE_KEY = 'course-list-page';

@Component({
  selector: 'app-course-list',
  imports: [
    DecimalPipe, FormsModule, RouterLink, TableModule, ButtonModule, DrawerModule,
    InputTextModule, SelectModule, DatePickerModule, TooltipModule
  ],
  templateUrl: './course-list.component.html',
  styleUrl: './course-list.component.scss'
})
export class CourseListComponent implements OnInit {
  private readonly service = inject(CourseService);
  private readonly lookup = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly items = signal<Course[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  protected readonly partners = signal<LookupItem[]>([]);
  protected readonly courseGroups = signal<LookupItem[]>([]);
  protected readonly publishStatuses = signal<LookupItem[]>([]);

  /** Bound to the filter drawer controls; only applied to the query on 查詢. */
  protected filters: CourseQuery = { ...EMPTY_COURSE_QUERY };
  protected dateFilters: DateFilters = { scheduleOnFrom: null, scheduleOnTo: null, scheduleOffFrom: null, scheduleOffTo: null };

  protected readonly boolOptions: { label: string; value: boolean | null }[] = [
    { label: '全部', value: null },
    { label: '是', value: true },
    { label: '否', value: false }
  ];

  protected sortField = 'displayOrder';
  protected sortOrder = 1;
  protected first = 0;
  protected rows = 20;
  protected readonly rowsPerPageOptions = [10, 20, 50];

  ngOnInit(): void {
    this.filters = { ...EMPTY_COURSE_QUERY, ...readSession<CourseQuery>(COURSE_LIST_FILTERS_KEY, {}) };

    // Cross-entity navigation (Partner / CourseGroup link buttons) overrides the saved filter of the same name.
    const params = this.route.snapshot.queryParamMap;
    const incomingPartner = params.get('partnerPkid');
    const incomingGroup = params.get('courseGroupPkid');
    if (incomingPartner !== null && incomingPartner !== '') {
      this.filters.partnerPkid = Number(incomingPartner);
    }
    if (incomingGroup !== null && incomingGroup !== '') {
      this.filters.courseGroupPkid = Number(incomingGroup);
    }
    this.syncDateFiltersFromQuery();

    const sort = readSession<SortState>(COURSE_LIST_SORT_KEY, { sortField: 'displayOrder', sortOrder: 1 });
    this.sortField = sort.sortField;
    this.sortOrder = sort.sortOrder;

    const page = readSession<PageState>(COURSE_LIST_PAGE_KEY, { first: 0, rows: 20 });
    this.first = page.first;
    this.rows = page.rows;

    // FK labels come with each row, so the list loads independently of the filter dropdown options.
    forkJoin({
      partners: this.lookup.partners(),
      courseGroups: this.lookup.courseGroups(),
      publishStatuses: this.lookup.publishStatuses()
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses }) => {
        this.partners.set(partners);
        this.courseGroups.set(courseGroups);
        this.publishStatuses.set(publishStatuses);
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
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得課程資料。' });
      }
    });
  }

  protected openFilters(): void {
    this.filterVisible.set(true);
  }

  protected search(): void {
    this.filters.scheduleOnFrom = toIso(this.dateFilters.scheduleOnFrom);
    this.filters.scheduleOnTo = toIso(this.dateFilters.scheduleOnTo);
    this.filters.scheduleOffFrom = toIso(this.dateFilters.scheduleOffFrom);
    this.filters.scheduleOffTo = toIso(this.dateFilters.scheduleOffTo);

    writeSession(COURSE_LIST_FILTERS_KEY, this.filters);
    this.first = 0;
    writeSession(COURSE_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
    this.filterVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.filters = { ...EMPTY_COURSE_QUERY };
    this.syncDateFiltersFromQuery();
    this.search();
  }

  /** Number of filters currently applied to the query; shown as a badge on the 搜尋條件 button. */
  protected get activeFilterCount(): number {
    const f = this.filters;
    return [
      !!f.keyword?.trim(),
      f.partnerPkid !== null && f.partnerPkid !== undefined,
      f.courseGroupPkid !== null && f.courseGroupPkid !== undefined,
      f.publishStatusPkid !== null && f.publishStatusPkid !== undefined,
      !!f.scheduleOnFrom || !!f.scheduleOnTo,
      !!f.scheduleOffFrom || !!f.scheduleOffTo,
      f.canRepeat !== null && f.canRepeat !== undefined
    ].filter(Boolean).length;
  }

  /** Badge text for the 搜尋條件 button; undefined hides the badge when nothing is filtered. */
  protected get filterBadge(): string | undefined {
    const count = this.activeFilterCount;
    return count > 0 ? `${count}` : undefined;
  }

  protected onSort(event: { field?: string; order?: number }): void {
    this.sortField = event.field ?? 'displayOrder';
    this.sortOrder = event.order ?? 1;
    writeSession(COURSE_LIST_SORT_KEY, { sortField: this.sortField, sortOrder: this.sortOrder });
  }

  protected onPage(event: TablePageEvent): void {
    this.first = event.first;
    this.rows = event.rows;
    writeSession(COURSE_LIST_PAGE_KEY, { first: this.first, rows: this.rows });
  }

  protected view(item: Course): void {
    this.router.navigate(['/course/courses', item.pkid]);
  }

  protected edit(item: Course): void {
    this.router.navigate(['/course/courses', item.pkid, 'edit']);
  }

  protected confirmDelete(item: Course): void {
    this.confirmation.confirm({
      header: '刪除課程',
      icon: 'pi pi-exclamation-triangle',
      message: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.courseId}」？`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.delete(item)
    });
  }

  private delete(item: Course): void {
    this.service.delete(item.pkid).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `主代碼 ${item.pkid}「${item.courseId}」已刪除。` });
        this.load();
      },
      error: err => {
        const detail = err?.status === 409
          ? (err.error?.message ?? '此課程仍被其他資料使用，無法刪除。')
          : '刪除失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }

  private syncDateFiltersFromQuery(): void {
    this.dateFilters = {
      scheduleOnFrom: fromIso(this.filters.scheduleOnFrom),
      scheduleOnTo: fromIso(this.filters.scheduleOnTo),
      scheduleOffFrom: fromIso(this.filters.scheduleOffFrom),
      scheduleOffTo: fromIso(this.filters.scheduleOffTo)
    };
  }
}
