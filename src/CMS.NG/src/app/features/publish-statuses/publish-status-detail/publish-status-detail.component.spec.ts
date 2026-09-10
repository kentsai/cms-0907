import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { PublishStatus } from '@core/models/publish-status.model';
import { PublishStatusService } from '@core/services/publish-status.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { PublishStatusDetailComponent } from './publish-status-detail.component';

describe('PublishStatusDetailComponent', () => {
  let service: jasmine.SpyObj<PublishStatusService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const item: PublishStatus = { pkid: 1, description: '草稿', isDraft: true, isPublished: false, isDiscontinued: false };

  async function setup(id = '1'): Promise<ComponentFixture<PublishStatusDetailComponent>> {
    await TestBed.configureTestingModule({
      imports: [PublishStatusDetailComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: PublishStatusService, useValue: service },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PublishStatusDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['getById', 'delete']);
    service.getById.and.returnValue(of(item));
    service.delete.and.returnValue(of(void 0));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRecord']);
    rowAudits.getForRecord.and.returnValue(of([
      { dateTime: '2026-09-07T01:02:03', userName: 'system', actionType: 'UPDATE', actionDesc: 'Description' }
    ]));
  });

  it('loads the record from the :id route param', async () => {
    await setup('1');
    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders the five fields', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('主代碼');
    expect(text).toContain('狀態說明');
    expect(text).toContain('草稿');
    expect(text).toContain('已發布');
    expect(text).toContain('已下架');
    expect(text).toContain('草稿');
  });

  it('renders both primary-foreign link buttons', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('查看課程');
    expect(text).toContain('查看活動');
  });

  it('shows the row-audit badge for the record', async () => {
    const fixture = await setup();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rowAudits.getForRecord).toHaveBeenCalledWith('PublishStatus', 1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Update by system');
  });

  it('shows a not-found message on 404', async () => {
    service.getById.and.returnValue(throwError(() => ({ status: 404 })));
    const fixture = await setup('99');

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('找不到主代碼 99');
  });
});
