import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { ToolbarModule } from 'primeng/toolbar';
import { Observable, forkJoin } from 'rxjs';
import { RowAuditBadgeComponent } from '@core/components/row-audit-badge/row-audit-badge.component';
import { CourseGroup, CourseGroupRequest } from '@core/models/course-group.model';
import { CourseGroupService } from '@core/services/course-group.service';

@Component({
  selector: 'app-course-group-form',
  imports: [ReactiveFormsModule, ButtonModule, CardModule, InputTextModule, ToolbarModule, RowAuditBadgeComponent],
  templateUrl: './course-group-form.component.html',
  styleUrl: './course-group-form.component.scss'
})
export class CourseGroupFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(CourseGroupService);
  private readonly messages = inject(MessageService);

  /** Edit mode when the route carries an :id; otherwise new mode. `pkid` is IDENTITY and never editable. */
  protected isEdit = false;
  protected pkid: number | null = null;

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    description: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(100)])
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit = id !== null;

    if (!this.isEdit) {
      return;
    }

    this.pkid = Number(id);
    this.loading.set(true);

    // forkJoin keeps the shape used by forms with FK lookups; this table has none, so only getById is joined.
    forkJoin({ item: this.service.getById(this.pkid) }).subscribe({
      next: ({ item }) => {
        this.form.patchValue({ description: item.description });
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.messages.add({
          severity: 'error',
          summary: '載入失敗',
          detail: err?.status === 404 ? `找不到主代碼 ${this.pkid} 的課程群組。` : '無法取得課程群組資料。'
        });
      }
    });
  }

  protected get title(): string {
    return this.isEdit ? '編輯課程群組' : '新增課程群組';
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }

  protected cancel(): void {
    if (this.isEdit && this.pkid !== null) {
      this.router.navigate(['/course/course-groups', this.pkid]);
    } else {
      this.router.navigate(['/course/course-groups']);
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const request: CourseGroupRequest = {
      pkid: this.pkid ?? 0,
      description: raw.description.trim()
    };

    this.saving.set(true);
    const call$: Observable<CourseGroup | void> = this.isEdit ? this.service.update(request) : this.service.create(request);

    call$.subscribe({
      next: created => {
        this.saving.set(false);
        // Create returns the row with its server-assigned pkid; update returns 204.
        const pkid = created ? created.pkid : request.pkid;
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `主代碼 ${pkid}「${request.description}」已儲存。` });
        this.router.navigate(['/course/course-groups', pkid]);
      },
      error: (err: { status?: number; error?: { message?: string } }) => {
        this.saving.set(false);
        let detail = '儲存失敗，請稍後再試。';
        if (err?.status === 404) {
          detail = `找不到主代碼 ${request.pkid} 的課程群組。`;
        } else if (err?.status === 400) {
          detail = '資料驗證失敗，請檢查欄位內容。';
        }
        this.messages.add({ severity: 'error', summary: '儲存失敗', detail });
      }
    });
  }
}
