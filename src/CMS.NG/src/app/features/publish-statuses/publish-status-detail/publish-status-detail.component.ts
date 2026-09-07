import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { RowAuditBadgeComponent } from '@core/components/row-audit-badge/row-audit-badge.component';
import { PublishStatus } from '@core/models/publish-status.model';
import { PublishStatusService } from '@core/services/publish-status.service';

@Component({
  selector: 'app-publish-status-detail',
  imports: [RouterLink, ButtonModule, CardModule, TagModule, ToolbarModule, RowAuditBadgeComponent],
  templateUrl: './publish-status-detail.component.html',
  styleUrl: './publish-status-detail.component.scss'
})
export class PublishStatusDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(PublishStatusService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly item = signal<PublishStatus | null>(null);
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
          this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得發布狀態資料。' });
        }
      }
    });
  }

  protected back(): void {
    this.router.navigate(['/admin/publish-statuses']);
  }

  protected edit(): void {
    this.router.navigate(['/admin/publish-statuses', this.pkid, 'edit']);
  }

  protected confirmDelete(): void {
    const item = this.item();
    if (!item) {
      return;
    }
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
        this.back();
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
