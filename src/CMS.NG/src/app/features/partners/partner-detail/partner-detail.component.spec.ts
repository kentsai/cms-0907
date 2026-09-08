import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { Partner } from '@core/models/partner.model';
import { PartnerService } from '@core/services/partner.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { PartnerDetailComponent } from './partner-detail.component';

describe('PartnerDetailComponent', () => {
  let service: jasmine.SpyObj<PartnerService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const item: Partner = {
    pkid: 1,
    name: 'Microsoft',
    appKey: 'MS',
    nameOnPartnerMenu: 'Microsoft 微軟',
    nameOnCourseDetailPage: '微軟',
    displayOrder: 10,
    imageFilename: null
  };

  async function setup(id = '1'): Promise<ComponentFixture<PartnerDetailComponent>> {
    await TestBed.configureTestingModule({
      imports: [PartnerDetailComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: PartnerService, useValue: service },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PartnerDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['getById', 'delete']);
    service.getById.and.returnValue(of(item));
    service.delete.and.returnValue(of(void 0));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRecord']);
    rowAudits.getForRecord.and.returnValue(of([
      { dateTime: '2026-09-07T01:02:03', userName: 'system', actionType: 'UPDATE', actionDesc: 'Name' }
    ]));
  });

  it('loads the record from the :id route param', async () => {
    await setup('1');
    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders the seven fields with a dash for the missing image', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('主代碼');
    expect(text).toContain('名稱');
    expect(text).toContain('應用程式代碼');
    expect(text).toContain('夥伴選單名稱');
    expect(text).toContain('課程頁顯示名稱');
    expect(text).toContain('顯示順序');
    expect(text).toContain('圖片檔名');
    expect(text).toContain('Microsoft 微軟');
    expect(text).toContain('—');
  });

  it('renders all five primary-foreign link buttons', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('查看課程');
    expect(text).toContain('查看認證');
    expect(text).toContain('查看課程群組');
    expect(text).toContain('查看說明會');
    expect(text).toContain('查看活動');
  });

  it('shows the row-audit badge for the record', async () => {
    const fixture = await setup();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rowAudits.getForRecord).toHaveBeenCalledWith('Partner', 1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Update by system');
  });

  it('shows a not-found message on 404', async () => {
    service.getById.and.returnValue(throwError(() => ({ status: 404 })));
    const fixture = await setup('99');

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('找不到主代碼 99');
  });

  it('asks for confirmation with pkid and name before deleting', async () => {
    const fixture = await setup();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const confirmation = TestBed.inject(ConfirmationService);
    spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>1</b>');
      expect(options.message).toContain('「Microsoft」');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: () => void }).confirmDelete();

    expect(service.delete).toHaveBeenCalledWith(1);
    expect(navigate).toHaveBeenCalledWith(['/course/partners']);
  });
});
