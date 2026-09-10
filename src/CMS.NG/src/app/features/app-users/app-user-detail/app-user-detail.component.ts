import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { forkJoin } from 'rxjs';
import { RowAuditBadgeComponent } from '@core/components/row-audit-badge/row-audit-badge.component';
import { AppUser } from '@core/models/app-user.model';
import { StringLookupItem } from '@core/models/lookup-item.model';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';

@Component({
  selector: 'app-app-user-detail',
  imports: [DatePipe, RouterLink, ButtonModule, CardModule, TagModule, ToolbarModule, RowAuditBadgeComponent],
  templateUrl: './app-user-detail.component.html',
  styleUrl: './app-user-detail.component.scss'
})
export class AppUserDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AppUserService);
  private readonly lookup = inject(LookupService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly item = signal<AppUser | null>(null);
  protected readonly roles = signal<StringLookupItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly resetting = signal(false);

  /** Assigned roles rendered with their RoleName; falls back to the raw id. */
  protected readonly assignedRoles = computed(() => {
    const byId = new Map(this.roles().map(r => [r.id, r.label]));
    return (this.item()?.roleIds ?? []).map(id => byId.get(id) ?? id);
  });

  protected userId = '';

  ngOnInit(): void {
    this.userId = this.route.snapshot.paramMap.get('id') ?? '';

    forkJoin({
      item: this.service.getById(this.userId),
      roles: this.lookup.appRoles()
    }).subscribe({
      next: ({ item, roles }) => {
        this.item.set(item);
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.notFound.set(err?.status === 404);
        if (err?.status !== 404) {
          this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得使用者資料。' });
        }
      }
    });
  }

  protected back(): void {
    this.router.navigate(['/admin/app-users']);
  }

  protected edit(): void {
    this.router.navigate(['/admin/app-users', this.userId, 'edit']);
  }

  protected confirmResetPassword(): void {
    const item = this.item();
    if (!item) {
      return;
    }
    this.confirmation.confirm({
      header: '重設密碼',
      icon: 'pi pi-key',
      message: `確定要將使用者「${item.userName}」（${item.userId}）的密碼重設為系統預設密碼？`,
      acceptLabel: '重設',
      rejectLabel: '取消',
      acceptButtonProps: { severity: 'warn' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.resetPassword(item)
    });
  }

  protected confirmDelete(): void {
    const item = this.item();
    if (!item) {
      return;
    }
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

  private resetPassword(item: AppUser): void {
    this.resetting.set(true);
    this.service.resetPassword(item.userId).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '密碼已重設', detail: `使用者「${item.userName}」的密碼已重設為系統預設密碼。` });
        // Reload so the new 密碼更新時間 shows.
        this.service.getById(item.userId).subscribe({
          next: refreshed => {
            this.item.set(refreshed);
            this.resetting.set(false);
          },
          error: () => this.resetting.set(false)
        });
      },
      error: (err: { status?: number; error?: { detail?: string; message?: string } }) => {
        this.resetting.set(false);
        const detail = err?.status === 500
          ? (err.error?.detail ?? '系統預設密碼設定有誤，請聯絡系統管理員。')
          : '重設密碼失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '重設失敗', detail });
      }
    });
  }

  private delete(item: AppUser): void {
    this.service.delete(item.userId).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `使用者「${item.userName}」已刪除。` });
        this.back();
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
