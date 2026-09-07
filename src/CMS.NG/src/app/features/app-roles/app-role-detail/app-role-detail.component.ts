import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { forkJoin } from 'rxjs';
import { RowAuditBadgeComponent } from '@core/components/row-audit-badge/row-audit-badge.component';
import { AppRole } from '@core/models/app-role.model';
import { StringLookupItem } from '@core/models/lookup-item.model';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';

@Component({
  selector: 'app-app-role-detail',
  imports: [ButtonModule, CardModule, TagModule, ToolbarModule, RowAuditBadgeComponent],
  templateUrl: './app-role-detail.component.html',
  styleUrl: './app-role-detail.component.scss'
})
export class AppRoleDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AppRoleService);
  private readonly lookup = inject(LookupService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly item = signal<AppRole | null>(null);
  protected readonly users = signal<StringLookupItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  /** Assigned users rendered with their lookup label; falls back to the raw id. */
  protected readonly assignedUsers = computed(() => {
    const byId = new Map(this.users().map(u => [u.id, u.label]));
    return (this.item()?.userIds ?? []).map(id => byId.get(id) ?? id);
  });

  protected roleId = '';

  ngOnInit(): void {
    this.roleId = this.route.snapshot.paramMap.get('id') ?? '';

    forkJoin({
      item: this.service.getById(this.roleId),
      users: this.lookup.appUsers()
    }).subscribe({
      next: ({ item, users }) => {
        this.item.set(item);
        this.users.set(users);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.notFound.set(err?.status === 404);
        if (err?.status !== 404) {
          this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得角色資料。' });
        }
      }
    });
  }

  protected back(): void {
    this.router.navigate(['/admin/app-roles']);
  }

  protected edit(): void {
    this.router.navigate(['/admin/app-roles', this.roleId, 'edit']);
  }

  protected confirmDelete(): void {
    const item = this.item();
    if (!item) {
      return;
    }
    const warning = item.userCount > 0 ? `<br/>（將同時移除 ${item.userCount} 位使用者的此角色）` : '';
    this.confirmation.confirm({
      header: '刪除角色',
      icon: 'pi pi-exclamation-triangle',
      message: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.roleName}」？${warning}`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.delete(item)
    });
  }

  private delete(item: AppRole): void {
    this.service.delete(item.roleId).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `角色「${item.roleName}」已刪除。` });
        this.back();
      },
      error: err => {
        const detail = err?.status === 409
          ? (err.error?.message ?? '此角色仍被其他資料使用，無法刪除。')
          : '刪除失敗，請稍後再試。';
        this.messages.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }
}
