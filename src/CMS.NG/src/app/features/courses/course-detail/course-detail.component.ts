import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ToolbarModule } from 'primeng/toolbar';
import { forkJoin } from 'rxjs';
import { QrCodeComponent } from '@core/components/qr-code/qr-code.component';
import { RowAuditBadgeComponent } from '@core/components/row-audit-badge/row-audit-badge.component';
import { Course, courseShowUrl } from '@core/models/course.model';
import { LookupItem } from '@core/models/lookup-item.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';

@Component({
  selector: 'app-course-detail',
  imports: [DecimalPipe, RouterLink, ButtonModule, CardModule, ToolbarModule, RowAuditBadgeComponent, QrCodeComponent],
  templateUrl: './course-detail.component.html',
  styleUrl: './course-detail.component.scss'
})
export class CourseDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(CourseService);
  private readonly lookup = inject(LookupService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly item = signal<Course | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  private readonly certifications = signal<LookupItem[]>([]);
  private readonly jobCategories = signal<LookupItem[]>([]);

  /** Certification labels for the linked pkids (falls back to the raw id if the lookup lacks it). */
  protected readonly certificationLabels = computed(() => this.labelsFor(this.item()?.certificationPkids ?? [], this.certifications()));
  protected readonly jobCategoryLabels = computed(() => this.labelsFor(this.item()?.jobCategoryPkids ?? [], this.jobCategories()));

  /** Public course page URL encoded in the QR code (`/Course/Show/{pkid}/{courseId}`). */
  protected readonly qrUrl = computed(() => {
    const item = this.item();
    return item ? courseShowUrl(item) : '';
  });

  protected pkid = 0;

  ngOnInit(): void {
    this.pkid = Number(this.route.snapshot.paramMap.get('id'));

    forkJoin({
      item: this.service.getById(this.pkid),
      certifications: this.lookup.certifications(),
      jobCategories: this.lookup.jobCategories()
    }).subscribe({
      next: ({ item, certifications, jobCategories }) => {
        this.item.set(item);
        this.certifications.set(certifications);
        this.jobCategories.set(jobCategories);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.notFound.set(err?.status === 404);
        if (err?.status !== 404) {
          this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得課程資料。' });
        }
      }
    });
  }

  protected back(): void {
    this.router.navigate(['/course/courses']);
  }

  protected edit(): void {
    this.router.navigate(['/course/courses', this.pkid, 'edit']);
  }

  protected confirmDelete(): void {
    const item = this.item();
    if (!item) {
      return;
    }
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
        this.back();
      },
      error: err => {
        const detail = err?.status === 409
          ? (err.error?.message ?? '此課程仍被其他資料使用，無法刪除。')
          : '刪除失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }

  private labelsFor(pkids: number[], options: LookupItem[]): string[] {
    const byPkid = new Map(options.map(o => [o.pkid, o.label]));
    return pkids.map(pkid => byPkid.get(pkid) ?? `#${pkid}`);
  }
}
