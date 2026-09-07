import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { ToolbarModule } from 'primeng/toolbar';
import { Observable, forkJoin, of } from 'rxjs';
import { RowAuditBadgeComponent } from '@core/components/row-audit-badge/row-audit-badge.component';
import { AppUser, AppUserRequest } from '@core/models/app-user.model';
import { StringLookupItem } from '@core/models/lookup-item.model';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';

@Component({
  selector: 'app-app-user-form',
  imports: [ReactiveFormsModule, ButtonModule, CardModule, CheckboxModule, InputTextModule, MultiSelectModule, ToolbarModule, RowAuditBadgeComponent],
  templateUrl: './app-user-form.component.html',
  styleUrl: './app-user-form.component.scss'
})
export class AppUserFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AppUserService);
  private readonly lookup = inject(LookupService);
  private readonly messages = inject(MessageService);

  /** Edit mode when the route carries an :id (UserId). `userId` is disabled in edit mode. */
  protected isEdit = false;
  protected userId: string | null = null;

  protected readonly roles = signal<StringLookupItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  /** No password control by design — the server seeds it from the system default. */
  protected readonly form = this.fb.nonNullable.group({
    userId: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    userName: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    isActive: this.fb.nonNullable.control(true),
    roleIds: this.fb.nonNullable.control<string[]>([])
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit = id !== null;
    this.userId = id;

    if (this.isEdit) {
      this.form.controls.userId.disable();
    }

    const item$: Observable<AppUser | null> = this.isEdit ? this.service.getById(id!) : of(null);

    this.loading.set(true);
    forkJoin({ roles: this.lookup.appRoles(), item: item$ }).subscribe({
      next: ({ roles, item }) => {
        this.roles.set(roles);
        if (item) {
          this.form.patchValue({
            userId: item.userId,
            userName: item.userName,
            isActive: item.isActive,
            roleIds: item.roleIds ?? []
          });
        }
        this.loading.set(false);
      },
      error: (err: { status?: number }) => {
        this.loading.set(false);
        this.messages.add({
          severity: 'error',
          summary: '載入失敗',
          detail: err?.status === 404 ? `找不到使用者代碼「${id}」的使用者。` : '無法取得使用者或角色資料。'
        });
      }
    });
  }

  protected get title(): string {
    return this.isEdit ? '編輯使用者' : '新增使用者';
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }

  protected cancel(): void {
    if (this.isEdit && this.userId !== null) {
      this.router.navigate(['/admin/app-users', this.userId]);
    } else {
      this.router.navigate(['/admin/app-users']);
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue() includes the disabled userId control in edit mode.
    const raw = this.form.getRawValue();
    const request: AppUserRequest = {
      userId: raw.userId.trim(),
      userName: raw.userName.trim(),
      isActive: raw.isActive,
      roleIds: raw.roleIds
    };

    this.saving.set(true);
    const call$: Observable<unknown> = this.isEdit ? this.service.update(request) : this.service.create(request);

    call$.subscribe({
      next: () => {
        this.saving.set(false);
        const detail = this.isEdit
          ? `使用者「${request.userName}」已儲存。`
          : `使用者「${request.userName}」已建立，密碼為系統預設密碼。`;
        this.messages.add({ severity: 'success', summary: '已儲存', detail });
        this.router.navigate(['/admin/app-users', request.userId]);
      },
      error: (err: { status?: number; error?: { message?: string; detail?: string } }) => {
        this.saving.set(false);
        let detail = '儲存失敗，請稍後再試。';
        if (err?.status === 409) {
          detail = err.error?.message ?? `使用者代碼「${request.userId}」已存在。`;
          this.form.controls.userId.setErrors({ duplicate: true });
        } else if (err?.status === 404) {
          detail = `找不到使用者代碼「${request.userId}」的使用者。`;
        } else if (err?.status === 400) {
          detail = '資料驗證失敗，請檢查欄位內容。';
        } else if (err?.status === 500) {
          detail = err.error?.detail ?? '系統預設密碼設定有誤，請聯絡系統管理員。';
        }
        this.messages.add({ severity: 'error', summary: '儲存失敗', detail });
      }
    });
  }
}
