import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { ToolbarModule } from 'primeng/toolbar';
import { Observable, forkJoin, of } from 'rxjs';
import { RowAuditBadgeComponent } from '@core/components/row-audit-badge/row-audit-badge.component';
import { AppRole, AppRoleRequest, DEFAULT_PERMISSION_LEVEL } from '@core/models/app-role.model';
import { StringLookupItem } from '@core/models/lookup-item.model';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';

@Component({
  selector: 'app-app-role-form',
  imports: [ReactiveFormsModule, ButtonModule, CardModule, InputNumberModule, InputTextModule, MultiSelectModule, ToolbarModule, RowAuditBadgeComponent],
  templateUrl: './app-role-form.component.html',
  styleUrl: './app-role-form.component.scss'
})
export class AppRoleFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AppRoleService);
  private readonly lookup = inject(LookupService);
  private readonly messages = inject(MessageService);

  /** Edit mode when the route carries an :id (RoleId). `roleId` is disabled in edit mode. */
  protected isEdit = false;
  protected roleId: string | null = null;

  protected readonly users = signal<StringLookupItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    roleId: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    roleName: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    permissionLevel: this.fb.control<number | null>(DEFAULT_PERMISSION_LEVEL, [Validators.required]),
    description: this.fb.control<string | null>(null, [Validators.maxLength(400)]),
    userIds: this.fb.nonNullable.control<string[]>([])
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit = id !== null;
    this.roleId = id;

    if (this.isEdit) {
      this.form.controls.roleId.disable();
    }

    const item$: Observable<AppRole | null> = this.isEdit ? this.service.getById(id!) : of(null);

    this.loading.set(true);
    forkJoin({ users: this.lookup.appUsers(), item: item$ }).subscribe({
      next: ({ users, item }) => {
        this.users.set(users);
        if (item) {
          this.form.patchValue({
            roleId: item.roleId,
            roleName: item.roleName,
            permissionLevel: item.permissionLevel,
            description: item.description,
            userIds: item.userIds ?? []
          });
        }
        this.loading.set(false);
      },
      error: (err: { status?: number }) => {
        this.loading.set(false);
        this.messages.add({
          severity: 'error',
          summary: '載入失敗',
          detail: err?.status === 404 ? `找不到角色代碼「${id}」的角色。` : '無法取得角色或使用者資料。'
        });
      }
    });
  }

  protected get title(): string {
    return this.isEdit ? '編輯角色' : '新增角色';
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }

  protected cancel(): void {
    if (this.isEdit && this.roleId !== null) {
      this.router.navigate(['/admin/app-roles', this.roleId]);
    } else {
      this.router.navigate(['/admin/app-roles']);
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue() includes the disabled roleId control in edit mode.
    const raw = this.form.getRawValue();
    const request: AppRoleRequest = {
      roleId: raw.roleId.trim(),
      roleName: raw.roleName.trim(),
      permissionLevel: Number(raw.permissionLevel ?? DEFAULT_PERMISSION_LEVEL),
      description: raw.description?.trim() ? raw.description.trim() : null,
      userIds: raw.userIds
    };

    this.saving.set(true);
    const call$: Observable<unknown> = this.isEdit ? this.service.update(request) : this.service.create(request);

    call$.subscribe({
      next: () => {
        this.saving.set(false);
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `角色「${request.roleName}」已儲存。` });
        this.router.navigate(['/admin/app-roles', request.roleId]);
      },
      error: (err: { status?: number; error?: { message?: string } }) => {
        this.saving.set(false);
        let detail = '儲存失敗，請稍後再試。';
        if (err?.status === 409) {
          detail = err.error?.message ?? `角色代碼「${request.roleId}」已存在。`;
          this.form.controls.roleId.setErrors({ duplicate: true });
        } else if (err?.status === 404) {
          detail = `找不到角色代碼「${request.roleId}」的角色。`;
        } else if (err?.status === 400) {
          detail = '資料驗證失敗，請檢查欄位內容。';
        }
        this.messages.add({ severity: 'error', summary: '儲存失敗', detail });
      }
    });
  }
}
