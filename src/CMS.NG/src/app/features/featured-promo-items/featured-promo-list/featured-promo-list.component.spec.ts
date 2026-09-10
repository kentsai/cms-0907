import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { FeaturedPromoClipboard, FeaturedPromoItem, FeaturedPromoWeek } from '@core/models/featured-promo-item.model';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import { startOfWeek, toIso } from '@core/utils/date.util';
import {
  BoardDay,
  BoardSlot,
  EditingCell,
  FEATURED_PROMO_LIST_FILTERS_KEY,
  FeaturedPromoListComponent
} from './featured-promo-list.component';

type ListInternals = {
  days: () => BoardDay[];
  weekLabel: () => string;
  editing: () => EditingCell | null;
  clipboard: () => FeaturedPromoClipboard | null;
  selectedCenter: () => number | null;
  onTabChange: (value: string | number | undefined) => void;
  previousWeek: () => void;
  nextWeek: () => void;
  thisWeek: () => void;
  startEdit: (day: BoardDay, slot: BoardSlot) => void;
  paste: (day: BoardDay, slot: BoardSlot) => void;
  copy: (item: FeaturedPromoItem) => void;
  onSaved: () => void;
  onCancelled: () => void;
  moveUp: (item: FeaturedPromoItem) => void;
  moveDown: (item: FeaturedPromoItem) => void;
  confirmDelete: (item: FeaturedPromoItem) => void;
};

describe('FeaturedPromoListComponent', () => {
  let service: jasmine.SpyObj<FeaturedPromoItemService>;
  let lookup: jasmine.SpyObj<LookupService>;

  const base: FeaturedPromoItem = {
    pkid: 1,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 50,
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
    trainingCenterName: '台北',
    promoCode: '20251204_SkillTrainAI'
  };

  const items: FeaturedPromoItem[] = [
    base,
    { ...base, pkid: 2, slot: 2, promotionPkid: 51, promoCode: '251211_GoogleAI', topic: 'Google AI工具一次掌握', description: '不需技術基礎' },
    { ...base, pkid: 3, scheduleOn: '2026-03-18', slot: 3, promotionPkid: 52, promoCode: '20251215_n8n', topic: 'n8n自動化三部曲', description: '從自動化新手到企業級AI架構師' }
  ];

  const week: FeaturedPromoWeek = { weekStart: '2026-03-16', weekEnd: '2026-03-22', trainingCenterPkid: 1, items };

  async function setup(): Promise<ComponentFixture<FeaturedPromoListComponent>> {
    await TestBed.configureTestingModule({
      imports: [FeaturedPromoListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: FeaturedPromoItemService, useValue: service },
        { provide: LookupService, useValue: lookup }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(FeaturedPromoListComponent);
    fixture.detectChanges();
    return fixture;
  }

  function internals(fixture: ComponentFixture<FeaturedPromoListComponent>): ListInternals {
    return fixture.componentInstance as unknown as ListInternals;
  }

  function saveWeek(weekStart: string, trainingCenterPkid: number | null = null): void {
    sessionStorage.setItem(FEATURED_PROMO_LIST_FILTERS_KEY, JSON.stringify({ trainingCenterPkid, weekStart }));
  }

  beforeEach(() => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<FeaturedPromoItemService>('FeaturedPromoItemService', ['getWeek', 'delete', 'moveUp', 'moveDown', 'create', 'update']);
    service.getWeek.and.returnValue(of(week));
    service.delete.and.returnValue(of(void 0));
    service.moveUp.and.returnValue(of(void 0));
    service.moveDown.and.returnValue(of(void 0));
    service.create.and.returnValue(of(base));
    service.update.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['trainingCenters', 'promotions']);
    lookup.trainingCenters.and.returnValue(of([
      { pkid: 1, label: '台北' }, { pkid: 2, label: '新竹' }, { pkid: 3, label: '台中' }
    ]));
    lookup.promotions.and.returnValue(of([]));
  });

  afterEach(() => sessionStorage.clear());

  it('loads the training centres, selects the first and requests the current week', async () => {
    const fixture = await setup();
    const c = internals(fixture);
    const expectedMonday = toIso(startOfWeek(new Date()))!;

    expect(lookup.trainingCenters).toHaveBeenCalled();
    expect(c.selectedCenter()).toBe(1);
    expect(service.getWeek).toHaveBeenCalledWith(1, expectedMonday);
    expect(JSON.parse(sessionStorage.getItem(FEATURED_PROMO_LIST_FILTERS_KEY)!)).toEqual({ trainingCenterPkid: 1, weekStart: expectedMonday });
  });

  it('renders one tab per training centre', async () => {
    const fixture = await setup();
    const tabs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('p-tab')).map(t => t.textContent?.trim());

    expect(tabs).toEqual(['台北', '新竹', '台中']);
  });

  it('restores the saved centre and snaps the saved date to its Monday', async () => {
    saveWeek('2026-03-18', 3);
    const fixture = await setup();
    const c = internals(fixture);

    expect(c.selectedCenter()).toBe(3);
    expect(service.getWeek).toHaveBeenCalledWith(3, '2026-03-16');
    expect(c.weekLabel()).toBe('3/16 -- 3/22');
  });

  it('falls back to the first centre when the saved one no longer exists', async () => {
    saveWeek('2026-03-16', 99);
    const fixture = await setup();

    expect(internals(fixture).selectedCenter()).toBe(1);
  });

  it('builds seven days × three slots and places items by (scheduleOn, slot)', async () => {
    saveWeek('2026-03-16');
    const fixture = await setup();
    const days = internals(fixture).days();

    expect(days.length).toBe(7);
    expect(days.map(d => d.label)).toEqual(['3/16 (一)', '3/17 (二)', '3/18 (三)', '3/19 (四)', '3/20 (五)', '3/21 (六)', '3/22 (日)']);
    expect(days.every(d => d.slots.length === 3)).toBeTrue();

    expect(days[0].slots[0].item?.pkid).toBe(1);
    expect(days[0].slots[1].item?.pkid).toBe(2);
    expect(days[0].slots[2].item).toBeNull();
    expect(days[2].slots[2].item?.pkid).toBe(3);
    expect(days[1].slots.every(s => s.item === null)).toBeTrue();
  });

  it('renders the day headers and the filled cells with PromoCode / Topic / Description', async () => {
    saveWeek('2026-03-16');
    const fixture = await setup();
    const host = fixture.nativeElement as HTMLElement;

    expect(host.querySelectorAll('.day__header').length).toBe(7);
    expect(host.querySelectorAll('.row').length).toBe(21);
    expect(host.querySelectorAll('.row--filled').length).toBe(3);
    expect(host.querySelectorAll('.row--empty').length).toBe(18);

    const firstDay = host.querySelector('[data-day="2026-03-16"]')!;
    const firstRow = firstDay.querySelector('.row--filled')!;
    expect(firstRow.textContent).toContain('20251204_SkillTrainAI');
    expect(firstRow.textContent).toContain('成為能AI協作的程式設計師');
    expect(firstRow.textContent).toContain('轉職就業養成班');
  });

  it('moves to the previous / next week and back to this week, persisting the Monday', async () => {
    saveWeek('2026-03-16');
    const fixture = await setup();
    const c = internals(fixture);

    c.nextWeek();
    expect(service.getWeek).toHaveBeenCalledWith(1, '2026-03-23');
    expect(c.weekLabel()).toBe('3/23 -- 3/29');

    c.previousWeek();
    c.previousWeek();
    expect(service.getWeek).toHaveBeenCalledWith(1, '2026-03-09');
    expect(JSON.parse(sessionStorage.getItem(FEATURED_PROMO_LIST_FILTERS_KEY)!).weekStart).toBe('2026-03-09');

    c.thisWeek();
    expect(service.getWeek).toHaveBeenCalledWith(1, toIso(startOfWeek(new Date()))!);
  });

  it('reloads for the chosen centre when a tab is activated and ignores a no-op change', async () => {
    saveWeek('2026-03-16');
    const fixture = await setup();
    const c = internals(fixture);
    const before = service.getWeek.calls.count();

    c.onTabChange(2);
    expect(c.selectedCenter()).toBe(2);
    expect(service.getWeek).toHaveBeenCalledWith(2, '2026-03-16');
    expect(JSON.parse(sessionStorage.getItem(FEATURED_PROMO_LIST_FILTERS_KEY)!).trainingCenterPkid).toBe(2);

    c.onTabChange(2);
    expect(service.getWeek.calls.count()).toBe(before + 1);
  });

  it('opens the inline form for an existing cell (edit) and for an empty cell (new)', async () => {
    saveWeek('2026-03-16');
    const fixture = await setup();
    const c = internals(fixture);
    const days = c.days();

    c.startEdit(days[0], days[0].slots[0]);
    fixture.detectChanges();
    expect(c.editing()).toEqual({ iso: '2026-03-16', slot: 1, item: items[0], initial: null });
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('app-featured-promo-form').length).toBe(1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('編輯版位 1');

    c.startEdit(days[1], days[1].slots[2]);
    fixture.detectChanges();
    expect(c.editing()).toEqual({ iso: '2026-03-17', slot: 3, item: null, initial: null });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('新增版位 3');

    c.onCancelled();
    fixture.detectChanges();
    expect(c.editing()).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('app-featured-promo-form').length).toBe(0);
  });

  it('copies a filled cell and pastes it into an empty cell as the form initial value', async () => {
    saveWeek('2026-03-16');
    const fixture = await setup();
    const c = internals(fixture);
    const days = c.days();

    expect(c.clipboard()).toBeNull();
    c.paste(days[1], days[1].slots[0]);
    expect(c.editing()).toBeNull();

    c.copy(items[1]);
    expect(c.clipboard()).toEqual({ promotionPkid: 51, promoCode: '251211_GoogleAI', topic: 'Google AI工具一次掌握', description: '不需技術基礎' });

    // Paste onto a filled cell is a no-op; onto an empty cell it opens the form pre-filled.
    c.paste(days[0], days[0].slots[0]);
    expect(c.editing()).toBeNull();

    c.paste(days[1], days[1].slots[0]);
    expect(c.editing()).toEqual({ iso: '2026-03-17', slot: 1, item: null, initial: c.clipboard() });
  });

  it('closes the form and reloads the week after a save', async () => {
    saveWeek('2026-03-16');
    const fixture = await setup();
    const c = internals(fixture);
    const before = service.getWeek.calls.count();

    c.startEdit(c.days()[0], c.days()[0].slots[2]);
    c.onSaved();

    expect(c.editing()).toBeNull();
    expect(service.getWeek.calls.count()).toBe(before + 1);
  });

  it('moves a row up / down through the service and reloads, skipping the bounds', async () => {
    saveWeek('2026-03-16');
    const fixture = await setup();
    const c = internals(fixture);
    const before = service.getWeek.calls.count();

    c.moveDown(items[0]); // slot 1 → 2
    expect(service.moveDown).toHaveBeenCalledWith(1);

    c.moveUp(items[1]); // slot 2 → 1
    expect(service.moveUp).toHaveBeenCalledWith(2);
    expect(service.getWeek.calls.count()).toBe(before + 2);

    c.moveUp(items[0]); // already on slot 1
    c.moveDown(items[2]); // already on slot 3
    expect(service.moveUp).toHaveBeenCalledTimes(1);
    expect(service.moveDown).toHaveBeenCalledTimes(1);
  });

  it('asks for confirmation with the day, slot and PromoCode before deleting', async () => {
    saveWeek('2026-03-16');
    const fixture = await setup();
    const c = internals(fixture);
    const confirmation = TestBed.inject(ConfirmationService);
    const confirmSpy = spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('3/18 (三)');
      expect(options.message).toContain('<b>3</b>');
      expect(options.message).toContain('「20251215_n8n」');
      options.accept?.();
      return confirmation;
    });
    const before = service.getWeek.calls.count();

    c.confirmDelete(items[2]);

    expect(confirmSpy).toHaveBeenCalled();
    expect(service.delete).toHaveBeenCalledWith(3);
    expect(service.getWeek.calls.count()).toBe(before + 1);
  });
});
