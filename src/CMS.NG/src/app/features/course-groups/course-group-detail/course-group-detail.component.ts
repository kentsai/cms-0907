import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ToolbarModule } from 'primeng/toolbar';
import { RowAuditBadgeComponent } from '@core/components/row-audit-badge/row-audit-badge.component';
import { CourseGroup } from '@core/models/course-group.model';
import { CourseGroupService } from '@core/services/course-group.service';

@Component({
  selector: 'app-course-group-detail',
  imports: [RouterLink, ButtonModule, CardModule, ToolbarModule, RowAuditBadgeComponent],
  templateUrl: './course-group-detail.component.html',
  styleUrl: './course-group-detail.component.scss'
})
export class CourseGroupDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(CourseGroupService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly item = signal<CourseGroup | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  protected pkid = 0;

  ngOnInit(): void {
    this.pkid = Number(this.route.snapshot.paramMap.get('id'));
    this.service.getById(this.pkid).subscribe({
      next: item => {
        this.item.set(item);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.notFound.set(err?.status === 404);
        if (err?.status !== 404) {
          this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得課程群組資料。' });
        }
      }
    });
  }

  protected back(): void {
    this.router.navigate(['/course/course-groups']);
  }

  protected edit(): void {
    this.router.navigate(['/course/course-groups', this.pkid, 'edit']);
  }

  protected confirmDelete(): void {
    const item = this.item();
    if (!item) {
      return;
    }
    this.confirmation.confirm({
      header: '刪除課程群組',
      icon: 'pi pi-exclamation-triangle',
      message: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.delete(item)
    });
  }

  private delete(item: CourseGroup): void {
    this.service.delete(item.pkid).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `主代碼 ${item.pkid}「${item.description}」已刪除。` });
        this.back();
      },
      error: err => {
        const detail = err?.status === 409
          ? (err.error?.message ?? '此課程群組仍被其他資料使用，無法刪除。')
          : '刪除失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }
}
