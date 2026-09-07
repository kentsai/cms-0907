import { Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ToolbarModule } from 'primeng/toolbar';
import { Observable, forkJoin, of } from 'rxjs';
import { RowAuditBadgeComponent } from '@core/components/row-audit-badge/row-audit-badge.component';
import { Course, CourseRequest } from '@core/models/course.model';
import { LookupItem } from '@core/models/lookup-item.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { asciiValidator } from '@core/utils/ascii.validator';
import { addYears, fromIso, toIso } from '@core/utils/date.util';

/** Years added to ScheduleOn to pre-fill ScheduleOff. */
export const SCHEDULE_OFF_DEFAULT_YEARS = 10;

/** Group-level validator: scheduleOff must not precede scheduleOn. Reports `{ scheduleRange: true }`. */
function scheduleRangeValidator(group: AbstractControl): ValidationErrors | null {
  const on = group.get('scheduleOn')?.value as Date | null;
  const off = group.get('scheduleOff')?.value as Date | null;
  if (!(on instanceof Date) || !(off instanceof Date)) {
    return null;
  }
  return off.getTime() < on.getTime() ? { scheduleRange: true } : null;
}

@Component({
  selector: 'app-course-form',
  imports: [
    ReactiveFormsModule, ButtonModule, CardModule, CheckboxModule, DatePickerModule, InputNumberModule,
    InputTextModule, MultiSelectModule, SelectModule, TextareaModule, ToolbarModule, RowAuditBadgeComponent
  ],
  templateUrl: './course-form.component.html',
  styleUrl: './course-form.component.scss'
})
export class CourseFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(CourseService);
  private readonly lookup = inject(LookupService);
  private readonly messages = inject(MessageService);

  /** Edit mode when the route carries an :id; otherwise new mode. `pkid` is IDENTITY and never editable. */
  protected isEdit = false;
  protected pkid: number | null = null;

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  protected readonly partners = signal<LookupItem[]>([]);
  protected readonly courseGroups = signal<LookupItem[]>([]);
  protected readonly publishStatuses = signal<LookupItem[]>([]);
  protected readonly certifications = signal<LookupItem[]>([]);
  protected readonly jobCategories = signal<LookupItem[]>([]);

  protected readonly form = this.fb.group({
    title: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    officialTitle: this.fb.control<string | null>(null, [Validators.maxLength(300)]),
    courseId: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50), asciiValidator]),
    prodCourseId: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50), asciiValidator]),
    friendlyUrl: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(100)]),
    displayOrder: this.fb.control<number | null>(0, [Validators.required]),
    partnerPkid: this.fb.control<number | null>(null, [Validators.required]),
    courseGroupPkid: this.fb.control<number | null>(null),
    publishStatusPkid: this.fb.control<number | null>(null, [Validators.required]),
    scheduleOn: this.fb.control<Date | null>(null, [Validators.required]),
    scheduleOff: this.fb.control<Date | null>(null, [Validators.required]),
    hour: this.fb.control<number | null>(0, [Validators.required, Validators.min(0)]),
    listPrice: this.fb.control<number | null>(0, [Validators.required, Validators.min(0)]),
    learningCredit: this.fb.control<number | null>(0, [Validators.required, Validators.min(0)]),
    material: this.fb.control<string | null>(null, [Validators.maxLength(500)]),
    objective: this.fb.control<string | null>(null, [Validators.maxLength(4000)]),
    target: this.fb.control<string | null>(null, [Validators.maxLength(500)]),
    prerequisites: this.fb.control<string | null>(null, [Validators.maxLength(4000)]),
    outline: this.fb.control<string | null>(null),
    towardCertOrExam: this.fb.control<string | null>(null),
    note: this.fb.control<string | null>(null, [Validators.maxLength(4000)]),
    otherInfo: this.fb.control<string | null>(null, [Validators.maxLength(4000)]),
    canRepeat: this.fb.nonNullable.control(false),
    certificationPkids: this.fb.nonNullable.control<number[]>([]),
    jobCategoryPkids: this.fb.nonNullable.control<number[]>([])
  }, { validators: [scheduleRangeValidator] });

  constructor() {
    // ScheduleOff defaults to ScheduleOn + 10 years whenever ScheduleOn changes to a real date.
    // emitEvent:false keeps the group validator / valueChanges from re-entering.
    this.form.controls.scheduleOn.valueChanges.subscribe(value => {
      if (value instanceof Date && !Number.isNaN(value.getTime())) {
        this.form.controls.scheduleOff.setValue(addYears(value, SCHEDULE_OFF_DEFAULT_YEARS), { emitEvent: false });
        this.form.updateValueAndValidity({ emitEvent: false });
      }
    });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit = id !== null;

    if (!this.isEdit) {
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      this.form.controls.scheduleOn.setValue(today); // triggers the +10y default for scheduleOff
    } else {
      this.pkid = Number(id);
    }

    const item$: Observable<Course | null> = this.isEdit ? this.service.getById(this.pkid!) : of(null);

    this.loading.set(true);
    forkJoin({
      partners: this.lookup.partners(),
      courseGroups: this.lookup.courseGroups(),
      publishStatuses: this.lookup.publishStatuses(),
      certifications: this.lookup.certifications(),
      jobCategories: this.lookup.jobCategories(),
      item: item$
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses, certifications, jobCategories, item }) => {
        this.partners.set(partners);
        this.courseGroups.set(courseGroups);
        this.publishStatuses.set(publishStatuses);
        this.certifications.set(certifications);
        this.jobCategories.set(jobCategories);

        if (item) {
          // scheduleOn is patched before scheduleOff, so the stored scheduleOff wins over the +10y default.
          this.form.patchValue({
            title: item.title,
            officialTitle: item.officialTitle,
            courseId: item.courseId,
            prodCourseId: item.prodCourseId,
            friendlyUrl: item.friendlyUrl,
            displayOrder: item.displayOrder,
            partnerPkid: item.partnerPkid,
            courseGroupPkid: item.courseGroupPkid,
            publishStatusPkid: item.publishStatusPkid,
            scheduleOn: fromIso(item.scheduleOn),
            scheduleOff: fromIso(item.scheduleOff),
            hour: item.hour,
            listPrice: item.listPrice,
            learningCredit: item.learningCredit,
            material: item.material,
            objective: item.objective,
            target: item.target,
            prerequisites: item.prerequisites,
            outline: item.outline,
            towardCertOrExam: item.towardCertOrExam,
            note: item.note,
            otherInfo: item.otherInfo,
            canRepeat: item.canRepeat,
            certificationPkids: item.certificationPkids ?? [],
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
          detail: err?.status === 404 ? `找不到主代碼 ${this.pkid} 的課程。` : '無法取得課程或選項資料。'
        });
      }
    });
  }

  protected get title(): string {
    return this.isEdit ? '編輯課程' : '新增課程';
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }

  protected get scheduleRangeInvalid(): boolean {
    const off = this.form.controls.scheduleOff;
    return this.form.hasError('scheduleRange') && (off.touched || off.dirty || this.form.controls.scheduleOn.dirty);
  }

  protected cancel(): void {
    if (this.isEdit && this.pkid !== null) {
      this.router.navigate(['/course/courses', this.pkid]);
    } else {
      this.router.navigate(['/course/courses']);
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const request: CourseRequest = {
      pkid: this.pkid ?? 0,
      title: raw.title.trim(),
      officialTitle: blankToNull(raw.officialTitle),
      courseId: raw.courseId.trim(),
      prodCourseId: raw.prodCourseId.trim(),
      friendlyUrl: raw.friendlyUrl.trim(),
      displayOrder: Number(raw.displayOrder),
      partnerPkid: Number(raw.partnerPkid),
      courseGroupPkid: raw.courseGroupPkid ?? null,
      publishStatusPkid: Number(raw.publishStatusPkid),
      scheduleOn: toIso(raw.scheduleOn)!,
      scheduleOff: toIso(raw.scheduleOff)!,
      hour: Number(raw.hour),
      listPrice: Number(raw.listPrice),
      learningCredit: Number(raw.learningCredit),
      material: blankToNull(raw.material),
      objective: blankToNull(raw.objective),
      target: blankToNull(raw.target),
      prerequisites: blankToNull(raw.prerequisites),
      outline: blankToNull(raw.outline),
      towardCertOrExam: blankToNull(raw.towardCertOrExam),
      note: blankToNull(raw.note),
      otherInfo: blankToNull(raw.otherInfo),
      canRepeat: raw.canRepeat,
      certificationPkids: raw.certificationPkids,
      jobCategoryPkids: raw.jobCategoryPkids
    };

    this.saving.set(true);
    const call$: Observable<Course | void> = this.isEdit ? this.service.update(request) : this.service.create(request);

    call$.subscribe({
      next: created => {
        this.saving.set(false);
        // Create returns the row with its server-assigned pkid; update returns 204.
        const pkid = created ? created.pkid : request.pkid;
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `主代碼 ${pkid}「${request.courseId}」已儲存。` });
        this.router.navigate(['/course/courses', pkid]);
      },
      error: (err: { status?: number; error?: { message?: string } }) => {
        this.saving.set(false);
        let detail = '儲存失敗，請稍後再試。';
        if (err?.status === 404) {
          detail = `找不到主代碼 ${request.pkid} 的課程。`;
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
