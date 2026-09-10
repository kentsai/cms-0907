import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { AutoCompleteCompleteEvent, AutoCompleteModule, AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { Observable } from 'rxjs';
import {
  FeaturedPromoClipboard,
  FeaturedPromoItem,
  FeaturedPromoItemRequest
} from '@core/models/featured-promo-item.model';
import { PromotionLookupItem } from '@core/models/lookup-item.model';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';

/**
 * Inline editor for one (day, centre, slot) cell of the FeaturedPromoItem board. Edit mode when `item` is
 * supplied, otherwise new mode; `initial` pre-fills a new row from the board's copy/paste clipboard.
 * The PromoCode field is an autocomplete over Promotion2 — choosing a code sets `promotionPkid` and pre-fills
 * blank Topic / Description from the promotion.
 */
@Component({
  selector: 'app-featured-promo-form',
  imports: [ReactiveFormsModule, AutoCompleteModule, ButtonModule, InputTextModule],
  templateUrl: './featured-promo-form.component.html',
  styleUrl: './featured-promo-form.component.scss'
})
export class FeaturedPromoFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookup = inject(LookupService);
  private readonly messages = inject(MessageService);

  /** `'yyyy-MM-dd'` of the day being edited. */
  readonly scheduleOn = input.required<string>();
  readonly trainingCenterPkid = input.required<number>();
  readonly slot = input.required<number>();
  /** Existing row (edit mode) or null (new mode). */
  readonly item = input<FeaturedPromoItem | null>(null);
  /** Clipboard values to pre-fill a new row (貼上). Ignored in edit mode. */
  readonly initial = input<FeaturedPromoClipboard | null>(null);

  /** Emits the saved row's pkid after a successful create/update. */
  readonly saved = output<number>();
  readonly cancelled = output<void>();

  protected readonly suggestions = signal<PromotionLookupItem[]>([]);
  protected readonly saving = signal(false);

  protected readonly form = this.fb.group({
    promotion: this.fb.control<PromotionLookupItem | null>(null, [Validators.required]),
    topic: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(100)]),
    description: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(300)])
  });

  ngOnInit(): void {
    const item = this.item();
    if (item) {
      this.form.patchValue({
        promotion: { pkid: item.promotionPkid, promoCode: item.promoCode, topic: item.topic, description: item.description },
        topic: item.topic,
        description: item.description
      });
      return;
    }

    const initial = this.initial();
    if (initial) {
      this.form.patchValue({
        promotion: { pkid: initial.promotionPkid, promoCode: initial.promoCode, topic: initial.topic, description: initial.description },
        topic: initial.topic,
        description: initial.description
      });
    }
  }

  protected get isEdit(): boolean {
    return this.item() !== null;
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }

  protected search(event: AutoCompleteCompleteEvent): void {
    this.lookup.promotions(event.query).subscribe({
      next: rows => this.suggestions.set(rows),
      error: () => {
        this.suggestions.set([]);
        this.messages.add({ severity: 'warn', summary: '查詢失敗', detail: '無法取得促銷代碼。' });
      }
    });
  }

  /** After a PromoCode is chosen, fill Topic / Description from the promotion when they are still blank. */
  protected onPromotionSelected(event: AutoCompleteSelectEvent): void {
    const promotion = event.value as PromotionLookupItem | null;
    if (!promotion) {
      return;
    }
    if (!this.form.controls.topic.value.trim()) {
      this.form.controls.topic.setValue(promotion.topic);
    }
    if (!this.form.controls.description.value.trim()) {
      this.form.controls.description.setValue(promotion.description);
    }
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const request: FeaturedPromoItemRequest = {
      pkid: this.item()?.pkid ?? 0,
      scheduleOn: this.scheduleOn(),
      trainingCenterPkid: this.trainingCenterPkid(),
      slot: this.slot(),
      promotionPkid: raw.promotion!.pkid,
      topic: raw.topic.trim(),
      description: raw.description.trim()
    };

    this.saving.set(true);
    const call$: Observable<FeaturedPromoItem | void> = this.isEdit ? this.service.update(request) : this.service.create(request);

    call$.subscribe({
      next: created => {
        this.saving.set(false);
        const pkid = created ? created.pkid : request.pkid;
        this.messages.add({
          severity: 'success',
          summary: '已儲存',
          detail: `${request.scheduleOn} 版位 ${request.slot}「${raw.promotion!.promoCode}」已儲存。`
        });
        this.saved.emit(pkid);
      },
      error: (err: { status?: number; error?: { message?: string } }) => {
        this.saving.set(false);
        let detail = '儲存失敗，請稍後再試。';
        if (err?.status === 409) {
          detail = err.error?.message ?? '此版位已有資料，無法儲存。';
        } else if (err?.status === 404) {
          detail = `找不到主代碼 ${request.pkid} 的資料。`;
        } else if (err?.status === 400) {
          detail = '資料驗證失敗，請檢查欄位內容。';
        }
        this.messages.add({ severity: 'error', summary: '儲存失敗', detail });
      }
    });
  }
}
