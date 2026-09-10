import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { ToolbarModule } from 'primeng/toolbar';
import { Observable, forkJoin, of } from 'rxjs';
import { RowAuditBadgeComponent } from '@core/components/row-audit-badge/row-audit-badge.component';
import { Certification, CertificationRequest, certificationLabel } from '@core/models/certification.model';
import { LookupItem } from '@core/models/lookup-item.model';
import { CertificationService } from '@core/services/certification.service';
import { LookupService } from '@core/services/lookup.service';

@Component({
  selector: 'app-certification-form',
  imports: [
    ReactiveFormsModule, ButtonModule, CardModule, InputTextModule, MultiSelectModule, SelectModule,
    ToolbarModule, RowAuditBadgeComponent
  ],
  templateUrl: './certification-form.component.html',
  styleUrl: './certification-form.component.scss'
})
export class CertificationFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(CertificationService);
  private readonly lookup = inject(LookupService);
  private readonly messages = inject(MessageService);

  /** Edit mode when the route carries an :id; otherwise new mode. `pkid` is IDENTITY and never editable. */
  protected isEdit = false;
  protected pkid: number | null = null;

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  protected readonly partners = signal<LookupItem[]>([]);
  protected readonly courses = signal<LookupItem[]>([]);
  protected readonly jobCategories = signal<LookupItem[]>([]);

  protected readonly form = this.fb.group({
    partnerPkid: this.fb.control<number | null>(null, [Validators.required]),
    title: this.fb.control<string | null>(null, [Validators.maxLength(100)]),
    coursePkids: this.fb.nonNullable.control<number[]>([]),
    jobCategoryPkids: this.fb.nonNullable.control<number[]>([])
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit = id !== null;
    if (this.isEdit) {
      this.pkid = Number(id);
    }

    const item$: Observable<Certification | null> = this.isEdit ? this.service.getById(this.pkid!) : of(null);

    this.loading.set(true);
    forkJoin({
      partners: this.lookup.partners(),
      courses: this.lookup.courses(),
      jobCategories: this.lookup.jobCategories(),
      item: item$
    }).subscribe({
      next: ({ partners, courses, jobCategories, item }) => {
        this.partners.set(partners);
        this.courses.set(courses);
        this.jobCategories.set(jobCategories);

        if (item) {
          this.form.patchValue({
            partnerPkid: item.partnerPkid,
            title: item.title,
            coursePkids: item.coursePkids ?? [],
            jobCategoryPkids: item.jobCategoryPkids ?? []
          });
        }
        this.loading.set(false);
      },
      error: (err: { status?: number }) => {
        this.loading.set(false);
        this.messages.add({
          severity: 'error',
          summary: '載入失敗',
          detail: err?.status === 404 ? `找不到主代碼 ${this.pkid} 的認證。` : '無法取得認證或選項資料。'
        });
      }
    });
  }

  protected get title(): string {
    return this.isEdit ? '編輯認證' : '新增認證';
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }

  protected cancel(): void {
    if (this.isEdit && this.pkid !== null) {
      this.router.navigate(['/course/certifications', this.pkid]);
    } else {
      this.router.navigate(['/course/certifications']);
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const request: CertificationRequest = {
      pkid: this.pkid ?? 0,
      partnerPkid: Number(raw.partnerPkid),
      title: blankToNull(raw.title),
      coursePkids: raw.coursePkids,
      jobCategoryPkids: raw.jobCategoryPkids
    };

    this.saving.set(true);
    const call$: Observable<Certification | void> = this.isEdit ? this.service.update(request) : this.service.create(request);

    call$.subscribe({
      next: created => {
        this.saving.set(false);
        // Create returns the row with its server-assigned pkid; update returns 204.
        const pkid = created ? created.pkid : request.pkid;
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `主代碼 ${pkid}「${certificationLabel(request)}」已儲存。` });
        this.router.navigate(['/course/certifications', pkid]);
      },
      error: (err: { status?: number; error?: { message?: string } }) => {
        this.saving.set(false);
        let detail = '儲存失敗，請稍後再試。';
        if (err?.status === 404) {
          detail = `找不到主代碼 ${request.pkid} 的認證。`;
        } else if (err?.status === 400) {
          detail = '資料驗證失敗，請檢查欄位內容。';
        }
        this.messages.add({ severity: 'error', summary: '儲存失敗', detail });
      }
    });
  }
}

function blankToNull(value: string | null | undefined): string | null {
  const trimmed = (value ?? '').trim();
  return trimmed === '' ? null : trimmed;
}
