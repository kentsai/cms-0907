import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { ToolbarModule } from 'primeng/toolbar';
import { Observable, forkJoin } from 'rxjs';
import { RowAuditBadgeComponent } from '@core/components/row-audit-badge/row-audit-badge.component';
import { PublishStatusRequest } from '@core/models/publish-status.model';
import { PublishStatusService } from '@core/services/publish-status.service';

@Component({
  selector: 'app-publish-status-form',
  imports: [ReactiveFormsModule, ButtonModule, CardModule, CheckboxModule, InputNumberModule, InputTextModule, ToolbarModule, RowAuditBadgeComponent],
  templateUrl: './publish-status-form.component.html',
  styleUrl: './publish-status-form.component.scss'
})
export class PublishStatusFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(PublishStatusService);
  private readonly messages = inject(MessageService);

  /** Edit mode when the route carries an :id; otherwise new mode. `pkid` is disabled in edit mode. */
  protected isEdit = false;
  protected pkid: number | null = null;

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    pkid: this.fb.control<number | null>(null, [Validators.required, Validators.min(0), Validators.max(255)]),
    description: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
    isDraft: this.fb.nonNullable.control(false),
    isPublished: this.fb.nonNullable.control(false),
    isDiscontinued: this.fb.nonNullable.control(false)
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit = id !== null;

    if (!this.isEdit) {
      return;
    }

    this.pkid = Number(id);
    this.form.controls.pkid.disable();
    this.loading.set(true);

    // forkJoin keeps the shape used by forms with FK lookups; this table has none, so only getById is joined.
    forkJoin({ item: this.service.getById(this.pkid) }).subscribe({
      next: ({ item }) => {
        this.form.patchValue(item);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.messages.add({
          severity: 'error',
          summary: '載入失敗',
          detail: err?.status === 404 ? `找不到主代碼 ${this.pkid} 的發布狀態。` : '無法取得發布狀態資料。'
        });
      }
    });
  }

  protected get title(): string {
    return this.isEdit ? '編輯發布狀態' : '新增發布狀態';
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }

  protected cancel(): void {
    if (this.isEdit && this.pkid !== null) {
      this.router.navigate(['/admin/publish-statuses', this.pkid]);
    } else {
      this.router.navigate(['/admin/publish-statuses']);
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue() includes the disabled pkid control in edit mode.
    const raw = this.form.getRawValue();
    const request: PublishStatusRequest = {
      pkid: Number(raw.pkid),
      description: raw.description.trim(),
      isDraft: raw.isDraft,
      isPublished: raw.isPublished,
      isDiscontinued: raw.isDiscontinued
    };

    this.saving.set(true);
    const call$: Observable<unknown> = this.isEdit ? this.service.update(request) : this.service.create(request);

    call$.subscribe({
      next: () => {
        this.saving.set(false);
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `主代碼 ${request.pkid}「${request.description}」已儲存。` });
        this.router.navigate(['/admin/publish-statuses', request.pkid]);
      },
      error: (err: { status?: number; error?: { message?: string } }) => {
        this.saving.set(false);
        let detail = '儲存失敗，請稍後再試。';
        if (err?.status === 409) {
          detail = err.error?.message ?? `主代碼 ${request.pkid} 已存在。`;
          this.form.controls.pkid.setErrors({ duplicate: true });
        } else if (err?.status === 404) {
          detail = `找不到主代碼 ${request.pkid} 的發布狀態。`;
        } else if (err?.status === 400) {
          detail = '資料驗證失敗，請檢查欄位內容。';
        }
        this.messages.add({ severity: 'error', summary: '儲存失敗', detail });
      }
    });
  }
}
