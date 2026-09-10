import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { TabsModule } from 'primeng/tabs';
import { TooltipModule } from 'primeng/tooltip';
import {
  FEATURED_PROMO_MAX_SLOT,
  FEATURED_PROMO_MIN_SLOT,
  FEATURED_PROMO_SLOTS,
  FeaturedPromoClipboard,
  FeaturedPromoItem
} from '@core/models/featured-promo-item.model';
import { LookupItem } from '@core/models/lookup-item.model';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import { addDays, formatMonthDay, formatMonthDayWeekday, fromIso, startOfWeek, toIso } from '@core/utils/date.util';
import { readSession, writeSession } from '@core/utils/session-storage.util';
import { FeaturedPromoFormComponent } from '../featured-promo-form/featured-promo-form.component';

/** One slot row on the board: filled (`item`) or empty. */
export interface BoardSlot {
  slot: number;
  item: FeaturedPromoItem | null;
}

/** One day column of the Monday–Sunday board. */
export interface BoardDay {
  iso: string;
  label: string;
  slots: BoardSlot[];
}

/** The cell currently showing the inline form. */
export interface EditingCell {
  iso: string;
  slot: number;
  item: FeaturedPromoItem | null;
  initial: FeaturedPromoClipboard | null;
}

interface BoardState {
  trainingCenterPkid: number | null;
  weekStart: string | null;
}

export const FEATURED_PROMO_LIST_FILTERS_KEY = 'featured-promo-list-filters';

/**
 * Weekly home-page promotion board (上稿作業): one tab per training centre, a Monday–Sunday week navigator,
 * and three slots per day edited inline. Copy / paste moves a promotion between cells through an in-memory
 * clipboard; the arrow buttons swap slots via the API.
 */
@Component({
  selector: 'app-featured-promo-list',
  imports: [ButtonModule, TabsModule, TooltipModule, FeaturedPromoFormComponent],
  templateUrl: './featured-promo-list.component.html',
  styleUrl: './featured-promo-list.component.scss'
})
export class FeaturedPromoListComponent implements OnInit {
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookup = inject(LookupService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly trainingCenters = signal<LookupItem[]>([]);
  protected readonly selectedCenter = signal<number | null>(null);
  protected readonly weekStart = signal<Date>(startOfWeek(new Date()));
  protected readonly items = signal<FeaturedPromoItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly clipboard = signal<FeaturedPromoClipboard | null>(null);
  protected readonly editing = signal<EditingCell | null>(null);

  protected readonly minSlot = FEATURED_PROMO_MIN_SLOT;
  protected readonly maxSlot = FEATURED_PROMO_MAX_SLOT;

  /** `3/16 -- 3/22` */
  protected readonly weekLabel = computed(() => {
    const start = this.weekStart();
    return `${formatMonthDay(start)} -- ${formatMonthDay(addDays(start, 6))}`;
  });

  /** Seven days × three slots, with the loaded items placed by (scheduleOn, slot). */
  protected readonly days = computed<BoardDay[]>(() => {
    const start = this.weekStart();
    const byCell = new Map(this.items().map(i => [`${i.scheduleOn}#${i.slot}`, i]));
    return Array.from({ length: 7 }, (_, offset) => {
      const date = addDays(start, offset);
      const iso = toIso(date)!;
      return {
        iso,
        label: formatMonthDayWeekday(date),
        slots: FEATURED_PROMO_SLOTS.map(slot => ({ slot, item: byCell.get(`${iso}#${slot}`) ?? null }))
      };
    });
  });

  ngOnInit(): void {
    const saved = readSession<BoardState>(FEATURED_PROMO_LIST_FILTERS_KEY, { trainingCenterPkid: null, weekStart: null });
    const savedWeek = fromIso(saved.weekStart);
    if (savedWeek) {
      this.weekStart.set(startOfWeek(savedWeek));
    }

    this.lookup.trainingCenters().subscribe({
      next: centers => {
        this.trainingCenters.set(centers);
        const restored = centers.find(c => c.pkid === saved.trainingCenterPkid) ?? centers[0] ?? null;
        this.selectedCenter.set(restored?.pkid ?? null);
        this.persist();
        this.load();
      },
      error: () => this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得訓練中心清單。' })
    });
  }

  protected load(): void {
    const center = this.selectedCenter();
    if (center === null) {
      this.items.set([]);
      return;
    }

    this.loading.set(true);
    this.service.getWeek(center, toIso(this.weekStart())!).subscribe({
      next: week => {
        this.items.set(week.items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得上稿資料。' });
      }
    });
  }

  protected onTabChange(value: string | number | undefined): void {
    const pkid = value === undefined || value === null || value === '' ? null : Number(value);
    if (pkid === this.selectedCenter()) {
      return;
    }
    this.selectedCenter.set(pkid);
    this.editing.set(null);
    this.persist();
    this.load();
  }

  protected previousWeek(): void {
    this.goToWeek(addDays(this.weekStart(), -7));
  }

  protected nextWeek(): void {
    this.goToWeek(addDays(this.weekStart(), 7));
  }

  protected thisWeek(): void {
    this.goToWeek(startOfWeek(new Date()));
  }

  protected isEditing(day: BoardDay, slot: BoardSlot): boolean {
    const editing = this.editing();
    return editing !== null && editing.iso === day.iso && editing.slot === slot.slot;
  }

  /** 編輯 on a filled cell, or 新增 on an empty one. */
  protected startEdit(day: BoardDay, slot: BoardSlot): void {
    this.editing.set({ iso: day.iso, slot: slot.slot, item: slot.item, initial: null });
  }

  /** 貼上: open the inline form on an empty cell pre-filled from the clipboard. */
  protected paste(day: BoardDay, slot: BoardSlot): void {
    const clipboard = this.clipboard();
    if (!clipboard || slot.item) {
      return;
    }
    this.editing.set({ iso: day.iso, slot: slot.slot, item: null, initial: clipboard });
  }

  /** 複製: remember the promotion and text of a filled cell. */
  protected copy(item: FeaturedPromoItem): void {
    this.clipboard.set({
      promotionPkid: item.promotionPkid,
      promoCode: item.promoCode,
      topic: item.topic,
      description: item.description
    });
    this.messages.add({ severity: 'info', summary: '已複製', detail: `「${item.promoCode}」已複製，可在空白版位貼上。` });
  }

  protected onSaved(): void {
    this.editing.set(null);
    this.load();
  }

  protected onCancelled(): void {
    this.editing.set(null);
  }

  protected moveUp(item: FeaturedPromoItem): void {
    if (item.slot <= FEATURED_PROMO_MIN_SLOT) {
      return;
    }
    this.service.moveUp(item.pkid).subscribe({
      next: () => this.load(),
      error: () => this.messages.add({ severity: 'error', summary: '移動失敗', detail: '無法上移版位，請稍後再試。' })
    });
  }

  protected moveDown(item: FeaturedPromoItem): void {
    if (item.slot >= FEATURED_PROMO_MAX_SLOT) {
      return;
    }
    this.service.moveDown(item.pkid).subscribe({
      next: () => this.load(),
      error: () => this.messages.add({ severity: 'error', summary: '移動失敗', detail: '無法下移版位，請稍後再試。' })
    });
  }

  protected confirmDelete(item: FeaturedPromoItem): void {
    const day = formatMonthDayWeekday(fromIso(item.scheduleOn) ?? new Date());
    this.confirmation.confirm({
      header: '刪除上稿',
      icon: 'pi pi-exclamation-triangle',
      message: `確定要刪除 <b>${day}</b> 版位 <b>${item.slot}</b>「${item.promoCode}」？`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.delete(item)
    });
  }

  private delete(item: FeaturedPromoItem): void {
    this.service.delete(item.pkid).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `版位 ${item.slot}「${item.promoCode}」已刪除。` });
        if (this.editing()?.item?.pkid === item.pkid) {
          this.editing.set(null);
        }
        this.load();
      },
      error: () => this.messages.add({ severity: 'error', summary: '刪除失敗', detail: '刪除失敗，請稍後再試。' })
    });
  }

  private goToWeek(monday: Date): void {
    this.weekStart.set(monday);
    this.editing.set(null);
    this.persist();
    this.load();
  }

  private persist(): void {
    writeSession(FEATURED_PROMO_LIST_FILTERS_KEY, {
      trainingCenterPkid: this.selectedCenter(),
      weekStart: toIso(this.weekStart())
    } satisfies BoardState);
  }
}
