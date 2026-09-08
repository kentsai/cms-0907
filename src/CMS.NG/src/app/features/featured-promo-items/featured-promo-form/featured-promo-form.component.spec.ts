import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AutoCompleteCompleteEvent, AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { of, throwError } from 'rxjs';
import { FeaturedPromoClipboard, FeaturedPromoItem } from '@core/models/featured-promo-item.model';
import { PromotionLookupItem } from '@core/models/lookup-item.model';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import { FeaturedPromoFormComponent } from './featured-promo-form.component';

type FormInternals = {
  form: FeaturedPromoFormComponent['form'];
  isEdit: boolean;
  save: () => void;
  cancel: () => void;
  search: (event: AutoCompleteCompleteEvent) => void;
  onPromotionSelected: (event: AutoCompleteSelectEvent) => void;
};

describe('FeaturedPromoFormComponent', () => {
  let service: jasmine.SpyObj<FeaturedPromoItemService>;
  let lookup: jasmine.SpyObj<LookupService>;

  const promotion: PromotionLookupItem = {
    pkid: 50,
    promoCode: '20251204_SkillTrainAI',
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班，三大主流語言任你選'
  };

  const existing: FeaturedPromoItem = {
    pkid: 1,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 50,
    topic: '自訂主題',
    description: '自訂說明',
    trainingCenterName: '台北',
    promoCode: '20251204_SkillTrainAI'
  };

  async function setup(
    inputs: { item?: FeaturedPromoItem | null; initial?: FeaturedPromoClipboard | null; slot?: number } = {}
  ): Promise<ComponentFixture<FeaturedPromoFormComponent>> {
    await TestBed.configureTestingModule({
      imports: [FeaturedPromoFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: FeaturedPromoItemService, useValue: service },
        { provide: LookupService, useValue: lookup }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(FeaturedPromoFormComponent);
    fixture.componentRef.setInput('scheduleOn', '2026-03-16');
    fixture.componentRef.setInput('trainingCenterPkid', 1);
    fixture.componentRef.setInput('slot', inputs.slot ?? 2);
    fixture.componentRef.setInput('item', inputs.item ?? null);
    fixture.componentRef.setInput('initial', inputs.initial ?? null);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<FeaturedPromoItemService>('FeaturedPromoItemService', ['create', 'update']);
    service.create.and.returnValue(of({ ...existing, pkid: 9, slot: 2 }));
    service.update.and.returnValue(of(void 0));

    lookup = jasmine.createSpyObj<LookupService>('LookupService', ['promotions']);
    lookup.promotions.and.returnValue(of([promotion]));
  });

  describe('new mode', () => {
    it('starts empty and invalid, showing the 新增 title with the slot number', async () => {
      const fixture = await setup();
      const c = fixture.componentInstance as unknown as FormInternals;

      expect(c.isEdit).toBeFalse();
      expect(c.form.invalid).toBeTrue();
      expect(c.form.controls.promotion.value).toBeNull();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('新增版位 2');
    });

    it('searches promotions by the typed keyword and exposes them as suggestions', async () => {
      const fixture = await setup();
      const c = fixture.componentInstance as unknown as FormInternals;

      c.search({ originalEvent: new Event('input'), query: 'Skill' });

      expect(lookup.promotions).toHaveBeenCalledWith('Skill');
      expect((fixture.componentInstance as unknown as { suggestions: () => PromotionLookupItem[] }).suggestions()).toEqual([promotion]);
    });

    it('pre-fills blank Topic / Description from the chosen promotion', async () => {
      const fixture = await setup();
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.promotion.setValue(promotion);
      c.onPromotionSelected({ originalEvent: new Event('select'), value: promotion });

      expect(c.form.controls.topic.value).toBe(promotion.topic);
      expect(c.form.controls.description.value).toBe(promotion.description);
      expect(c.form.valid).toBeTrue();
    });

    it('does not overwrite Topic / Description the user already typed', async () => {
      const fixture = await setup();
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.topic.setValue('自訂主題');
      c.onPromotionSelected({ originalEvent: new Event('select'), value: promotion });

      expect(c.form.controls.topic.value).toBe('自訂主題');
      expect(c.form.controls.description.value).toBe(promotion.description);
    });

    it('rejects over-long text', async () => {
      const fixture = await setup();
      const c = fixture.componentInstance as unknown as FormInternals;

      c.form.controls.topic.setValue('名'.repeat(101));
      c.form.controls.description.setValue('名'.repeat(301));

      expect(c.form.controls.topic.hasError('maxlength')).toBeTrue();
      expect(c.form.controls.description.hasError('maxlength')).toBeTrue();
    });

    it('does not call the service when saving an invalid form', async () => {
      const fixture = await setup();
      const c = fixture.componentInstance as unknown as FormInternals;

      c.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(c.form.controls.promotion.touched).toBeTrue();
    });

    it('creates with pkid 0, the cell coordinates, the promotion pkid and trimmed text, then emits saved', async () => {
      const fixture = await setup();
      const c = fixture.componentInstance as unknown as FormInternals;
      let savedPkid: number | undefined;
      fixture.componentInstance.saved.subscribe(pkid => (savedPkid = pkid));

      c.form.controls.promotion.setValue(promotion);
      c.form.controls.topic.setValue('  主題  ');
      c.form.controls.description.setValue(' 說明 ');
      c.save();

      expect(service.create).toHaveBeenCalledWith({
        pkid: 0,
        scheduleOn: '2026-03-16',
        trainingCenterPkid: 1,
        slot: 2,
        promotionPkid: 50,
        topic: '主題',
        description: '說明'
      });
      expect(service.update).not.toHaveBeenCalled();
      expect(savedPkid).toBe(9);
    });

    it('pre-fills from the clipboard (貼上)', async () => {
      const clipboard: FeaturedPromoClipboard = {
        promotionPkid: 51,
        promoCode: '251211_GoogleAI',
        topic: 'Google AI工具一次掌握',
        description: '不需技術基礎'
      };
      const fixture = await setup({ initial: clipboard });
      const c = fixture.componentInstance as unknown as FormInternals;

      expect(c.form.controls.promotion.value?.pkid).toBe(51);
      expect(c.form.controls.promotion.value?.promoCode).toBe('251211_GoogleAI');
      expect(c.form.controls.topic.value).toBe('Google AI工具一次掌握');
      expect(c.form.controls.description.value).toBe('不需技術基礎');
      expect(c.form.valid).toBeTrue();
      expect(c.isEdit).toBeFalse();
    });

    it('shows the API message on a 409 slot conflict', async () => {
      service.create.and.returnValue(throwError(() => ({ status: 409, error: { message: '版位已有資料' } })));
      const fixture = await setup();
      const c = fixture.componentInstance as unknown as FormInternals;
      const add = spyOn(TestBed.inject(MessageService), 'add');

      c.form.controls.promotion.setValue(promotion);
      c.form.controls.topic.setValue('主題');
      c.form.controls.description.setValue('說明');
      c.save();

      expect(add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error', detail: '版位已有資料' }));
    });

    it('emits cancelled without saving', async () => {
      const fixture = await setup();
      const c = fixture.componentInstance as unknown as FormInternals;
      let cancelled = false;
      fixture.componentInstance.cancelled.subscribe(() => (cancelled = true));

      c.cancel();

      expect(cancelled).toBeTrue();
      expect(service.create).not.toHaveBeenCalled();
    });
  });

  describe('edit mode', () => {
    it('loads the existing row (ignoring any clipboard) and updates with the original pkid', async () => {
      const clipboard: FeaturedPromoClipboard = { promotionPkid: 51, promoCode: 'X', topic: 'X', description: 'X' };
      const fixture = await setup({ item: existing, initial: clipboard, slot: 1 });
      const c = fixture.componentInstance as unknown as FormInternals;
      let savedPkid: number | undefined;
      fixture.componentInstance.saved.subscribe(pkid => (savedPkid = pkid));

      expect(c.isEdit).toBeTrue();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('編輯版位 1');
      expect(c.form.controls.promotion.value?.pkid).toBe(50);
      expect(c.form.controls.promotion.value?.promoCode).toBe('20251204_SkillTrainAI');
      expect(c.form.controls.topic.value).toBe('自訂主題');
      expect(c.form.controls.description.value).toBe('自訂說明');

      c.form.controls.description.setValue('新的說明');
      c.save();

      expect(service.update).toHaveBeenCalledWith({
        pkid: 1,
        scheduleOn: '2026-03-16',
        trainingCenterPkid: 1,
        slot: 1,
        promotionPkid: 50,
        topic: '自訂主題',
        description: '新的說明'
      });
      expect(service.create).not.toHaveBeenCalled();
      expect(savedPkid).toBe(1);
    });
  });
});
