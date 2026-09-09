import { DOCUMENT, DecimalPipe } from '@angular/common';
import {
  Component,
  DestroyRef,
  Injector,
  OnDestroy,
  OnInit,
  ViewEncapsulation,
  afterNextRender,
  computed,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { QrCodeComponent } from '@core/components/qr-code/qr-code.component';
import { Course, courseShowUrl } from '@core/models/course.model';
import { CourseService } from '@core/services/course.service';
import { PublishStatusService } from '@core/services/publish-status.service';
import { toIso } from '@core/utils/date.util';

/** One 課程內容 block of the print view. */
export interface PrintSection {
  label: string;
  text: string;
}

/** The eight text blocks in print order (`spec\course\Course.md`, Print view). */
const SECTIONS: ReadonlyArray<{ label: string; key: keyof Course }> = [
  { label: '課程目標', key: 'objective' },
  { label: '適合對象', key: 'target' },
  { label: '先備知識', key: 'prerequisites' },
  { label: '教材', key: 'material' },
  { label: '課程大綱', key: 'outline' },
  { label: '考試／認證說明', key: 'towardCertOrExam' },
  { label: '備註', key: 'note' },
  { label: '其他資訊', key: 'otherInfo' }
];

/** The one section that may legitimately span pages (every other block stays on one page). */
export const FLOWING_SECTION_LABEL = '課程大綱';

/** Custom properties read by the `@page` margin boxes in `course-print.component.scss` (bottom-left / bottom-center). */
export const PRINT_COURSE_ID_PROPERTY = '--print-course-id';
export const PRINT_DATE_PROPERTY = '--print-date';

/** Quotes a value as a CSS `<string>` so `content: var(--x)` renders it (`"` and `\` escaped). */
export function cssString(value: string): string {
  return `"${value.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`;
}

/**
 * 列印PDF — customer-facing print view of one course, reached from the detail page in a new tab at
 * `/course/courses/:id/print` (a chromeless route: no shell, toast or confirm dialog around it). Shows only the
 * customer field set; internal fields (主代碼, 顯示順序, 友善網址, 上架狀態, dates, 認證, 職務類別, 相關資料) are
 * absent. Opens the browser's print dialog once the course (and, for published courses, its QR code) is ready;
 * the browser's save-as-PDF is the PDF.
 */
@Component({
  selector: 'app-course-print',
  imports: [DecimalPipe, ButtonModule, QrCodeComponent],
  templateUrl: './course-print.component.html',
  styleUrl: './course-print.component.scss',
  // Global styles: `@page` (footer margin boxes) and `@media print` cannot be scoped to a component.
  encapsulation: ViewEncapsulation.None,
  host: { class: 'course-print' }
})
export class CoursePrintComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly courses = inject(CourseService);
  private readonly publishStatuses = inject(PublishStatusService);

  protected readonly item = signal<Course | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly failed = signal(false);
  /** From `PublishStatus.isPublished`; the public page (and so the QR) only exists for published courses. */
  protected readonly isPublished = signal(false);

  protected readonly officialTitle = computed(() => this.item()?.officialTitle?.trim() ?? '');
  protected readonly courseGroup = computed(() => this.item()?.courseGroupDescription?.trim() ?? '');
  protected readonly qrUrl = computed(() => {
    const item = this.item();
    return item ? courseShowUrl(item) : '';
  });
  protected readonly showQr = computed(() => this.item() !== null && this.isPublished());
  /** Non-empty (after trimming) text blocks in print order. */
  protected readonly sections = computed<PrintSection[]>(() => {
    const item = this.item();
    if (!item) {
      return [];
    }
    return SECTIONS.flatMap(({ label, key }) => {
      const raw = item[key];
      const text = typeof raw === 'string' ? raw.trim() : '';
      return text ? [{ label, text }] : [];
    });
  });

  protected readonly flowingSectionLabel = FLOWING_SECTION_LABEL;
  protected pkid = 0;
  private printed = false;

  ngOnInit(): void {
    this.pkid = Number(this.route.snapshot.paramMap.get('id'));

    this.courses
      .getById(this.pkid)
      .pipe(
        switchMap(item =>
          this.publishStatuses.getById(item.publishStatusPkid).pipe(
            map(status => ({ item, isPublished: status.isPublished })),
            // The course loaded; a failed status lookup only costs the QR.
            catchError(() => of({ item, isPublished: false }))
          )
        ),
        // A page that is torn down (navigation away, or the tab closing) must never print afterwards.
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: ({ item, isPublished }) => {
          this.item.set(item);
          this.isPublished.set(isPublished);
          this.loading.set(false);
          // Chrome / Edge derive the default PDF file name from the document title.
          this.document.title = `${item.courseId} ${item.title}`;
          this.setPrintProperties(item.courseId, toIso(new Date()) ?? '');
          if (!isPublished) {
            // No QR to wait for.
            this.schedulePrint();
          }
        },
        error: err => {
          this.loading.set(false);
          if (err?.status === 404) {
            this.notFound.set(true);
          } else {
            this.failed.set(true);
          }
        }
      });
  }

  ngOnDestroy(): void {
    const style = this.document.documentElement.style;
    style.removeProperty(PRINT_COURSE_ID_PROPERTY);
    style.removeProperty(PRINT_DATE_PROPERTY);
  }

  /** The QR symbol is drawn or has failed (then it is hidden): the page is complete, print it. */
  protected onQrSettled(): void {
    this.schedulePrint();
  }

  /** 列印 button on the screen preview: re-opens the print dialog (e.g. after cancelling it). */
  protected print(): void {
    window.print();
  }

  /** Opens the print dialog once, after the next paint so the first print never captures a pre-layout page. */
  private schedulePrint(): void {
    if (this.printed) {
      return;
    }
    this.printed = true;
    afterNextRender(() => window.print(), { injector: this.injector });
  }

  private setPrintProperties(courseId: string, printDate: string): void {
    const style = this.document.documentElement.style;
    style.setProperty(PRINT_COURSE_ID_PROPERTY, cssString(courseId));
    style.setProperty(PRINT_DATE_PROPERTY, cssString(printDate));
  }
}
